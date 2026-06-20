using CloudPiiScannerService.Models;
using CloudPiiScannerService.Models.Enums;
using DnsClient.Protocol;
using MongoDB.Bson;
using MongoDB.Driver;

namespace CloudPiiScannerService.Services;

public interface IWorkerService
{
    Task<string> RegisterAgentAsync(AgentRegistrationRequest request, CancellationToken ct = default);
    Task<HeartbeatResponse> SaveHeartbeatAsync(HeartbeatRequest request, CancellationToken ct = default);
    Task InsertScanResultAsync(IEnumerable<ScanResults> result, CancellationToken ct = default);
}

public class WorkerService : IWorkerService
{
    private readonly IMongoCollection<AgentDocument> _agents;
    private readonly IMongoCollection<HeartbeatDocument> _heartbeats;
    private readonly IMongoCollection<ScanResultsDocument> _scanResults;
    private readonly IMongoCollection<ScanConfig> _scanConfigs;

    public WorkerService(IMongoDatabase database)
    {
        _agents = database.GetCollection<AgentDocument>("agents");
        _heartbeats = database.GetCollection<HeartbeatDocument>("heartbeats");
        _scanResults = database.GetCollection<ScanResultsDocument>("scanresults");
        _scanConfigs = database.GetCollection<ScanConfig>("scanconfigs");
    }

    public async Task<string> RegisterAgentAsync(AgentRegistrationRequest request, CancellationToken ct = default)
    {
        var existing = await _agents
         .Find(x => x.MachineName == request.MachineName)
         .FirstOrDefaultAsync(ct);

        if (existing != null)
            return existing.Id;

        var doc = new AgentDocument
        {
            Id = Guid.NewGuid().ToString(),
            MachineName = request.MachineName,
            CurrentUser = request.CurrentUser,
            MacAddress = request.MacAddress,
            OperatingSystem = request.OperatingSystem,
            OsVersion = request.OsVersion,
            AgentVersion = request.AgentVersion,
            RegisteredAt = DateTime.UtcNow
        };

        await _agents.InsertOneAsync(doc, cancellationToken: ct);
        return doc.Id;
    }

    public async Task<HeartbeatResponse> SaveHeartbeatAsync(HeartbeatRequest request, CancellationToken ct = default)
    {
        // Upsert heartbeat by AgentId: update timestamp/status/machine or insert new document
        var filter = Builders<HeartbeatDocument>.Filter.Eq(h => h.AgentId, request.AgentId);

        var update = Builders<HeartbeatDocument>.Update
            .Set(h => h.MachineName, request.MachineName)
            .Set(h => h.TimeStampUtc, DateTime.UtcNow)
            .Set(h => h.Status, "alive")
            .SetOnInsert(h => h.Id, Guid.NewGuid().ToString())
            .SetOnInsert(h => h.AgentId, request.AgentId);

        var options = new UpdateOptions { IsUpsert = true };

        await _heartbeats.UpdateOneAsync(filter, update, options, ct);
        var scanConfig = await GetScheduledScanByAgentIdAsync(request.AgentId);
        // For now return default polling interval
        return new HeartbeatResponse
        {
            AgentId = request.AgentId,
            PollingIntervalMinutes = 5,
            ScanConfig=scanConfig
        };
    }
    public async Task<ScanConfig> GetScheduledScanByAgentIdAsync(string agentId)
    {
        var filter = Builders<ScanConfig>.Filter.ElemMatch(
            x => x.AgentIds,
            agent => agent.AgentId == agentId && agent.Status == "Scheduled"
        );

        return await _scanConfigs.Find(filter).FirstOrDefaultAsync();
    }

    public async Task InsertScanResultAsync(IEnumerable<ScanResults> records, CancellationToken ct = default)
    {
        // Define filter to identify unique scan result: MachineName + Source + FilePath + Entity
        if (records is null) throw new ArgumentNullException(nameof(records));

        var list = new List<ScanResultsDocument>();
        foreach (var r in records)
        {
            if (r is null) continue;
            list.Add(new ScanResultsDocument
            {
                Id = Guid.NewGuid().ToString(),
                MachineName = r.MachineName,
                Source = r.Source,
                FilePath = r.FilePath,
                Entity = r.Entity,
                IsDetected = r.IsDetected,
                Details = r.Details,
                LastUpdatedUtc = DateTime.UtcNow
            });
        }

        if (list.Count == 0) return;

        // insert results
        await _scanResults.InsertManyAsync(list, cancellationToken: ct);

        // --- NEW: update scan status and per-agent status using ScanId from payload ---
        // Build mapping: machineName -> agentId (if agent exists)
        var machineNames = list.Select(x => x.MachineName).Distinct(StringComparer.OrdinalIgnoreCase);
        var machineToAgentId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mn in machineNames)
        {
            var agent = await _agents.Find(a => a.MachineName == mn).FirstOrDefaultAsync(ct);
            if (agent != null)
            {
                machineToAgentId[mn] = agent.Id;
            }
        }

        // Group incoming ScanResults (original payload) by ScanId (if provided) and update the ScanConfig record(s)
        // Note: ScanResults model includes ScanId; if missing, skip scan status update.
        var incomingByScanId = records
            .Where(r => r != null && !string.IsNullOrWhiteSpace(r.ScanId))
            .GroupBy(r => r.ScanId, StringComparer.OrdinalIgnoreCase);

        foreach (var group in incomingByScanId)
        {
            var scanId = group.Key;
            if (string.IsNullOrWhiteSpace(scanId)) continue;

            // Determine agent ids involved in this scan payload
            var affectedAgentIds = group
                .Select(r => r.MachineName)
                .Where(mn => !string.IsNullOrWhiteSpace(mn) && machineToAgentId.ContainsKey(mn))
                .Select(mn => machineToAgentId[mn])
                .Distinct()
                .ToList();

            if (affectedAgentIds.Count == 0)
            {
                // nothing to update for this scan
                continue;
            }

            var filter = Builders<ScanConfig>.Filter.Eq(s => s.Id, scanId);

            // Set overall scan status to Completed and update LastRun
            var baseUpdate = Builders<ScanConfig>.Update
                .Set(s => s.Status, "Completed")
                .Set(s => s.LastRun, DateTime.UtcNow);

            // Update scan status (once)
            await _scanConfigs.UpdateOneAsync(filter, baseUpdate, cancellationToken: ct);

            // For each agentId set the agent status inside the ScanConfig.AgentIds array to "Complete"
            foreach (var agentId in affectedAgentIds)
            {
                var arrayFilter = new BsonDocumentArrayFilterDefinition<BsonDocument>(
                    new BsonDocument("elem.agentId", agentId)
                );

                var updateAgentStatus = Builders<ScanConfig>.Update.Set("agentIds.$[elem].status", "Complete");

                var options = new UpdateOptions
                {
                    ArrayFilters = new List<ArrayFilterDefinition> { arrayFilter }
                };

                await _scanConfigs.UpdateOneAsync(filter, updateAgentStatus, options, ct);
            }
        }
    }
}

internal class AgentDocument
{
    public string Id { get; set; } = default!;
    public string MachineName { get; set; } = default!;
    public string CurrentUser { get; set; } = default!;
    public string MacAddress { get; set; } = default!;
    public string OperatingSystem { get; set; } = default!;
    public string OsVersion { get; set; } = default!;
    public string AgentVersion { get; set; } = default!;
    public DateTime RegisteredAt { get; set; }
}

internal class HeartbeatDocument
{
    public string Id { get; set; } = default!;
    public string AgentId { get; set; } = default!;
    public string MachineName { get; set; } = default!;
    public DateTime TimeStampUtc { get; set; }
    public string Status { get; set; } = default!;
}

internal class ScanResultsDocument
{
    public string Id { get; set; } = default!;
    public string MachineName { get; set; } = default!;
    public StorageSource Source { get; set; }
    public string FilePath { get; set; } = default!;
    public string Entity { get; set; } = default!;
    public bool IsDetected { get; set; }
    public string Details { get; set; } = default!;
    public DateTime LastUpdatedUtc { get; set; }
}
