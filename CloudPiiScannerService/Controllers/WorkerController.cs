using Microsoft.AspNetCore.Mvc;
using CloudPiiScannerService.Models;
using CloudPiiScannerService.Services;

namespace CloudPiiScannerService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WorkerController : ControllerBase
    {
        private readonly ILogger<WorkerController> _logger;
        private readonly IWorkerService _workerService;

        public WorkerController(ILogger<WorkerController> logger, IWorkerService workerService)
        {
            _logger = logger;
            _workerService = workerService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> RegisterMachine([FromBody] AgentRegistrationRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            // Basic validation
            if (string.IsNullOrWhiteSpace(request.MachineName) ||
                string.IsNullOrWhiteSpace(request.CurrentUser) ||
                string.IsNullOrWhiteSpace(request.MacAddress))
            {
                return BadRequest("MachineName, CurrentUser and MacAddress are required.");
            }

            _logger.LogInformation("Registering machine {MachineName} ({MacAddress}) by {CurrentUser}",
                request.MachineName, request.MacAddress, request.CurrentUser);

            var id = await _workerService.RegisterAgentAsync(request, HttpContext.RequestAborted);

            return Ok(new { AgentId = id });
        }

        [HttpPost("heartbeat")]
        public async Task<IActionResult> Heartbeat([FromBody] HeartbeatRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.AgentId) || string.IsNullOrWhiteSpace(request.MachineName))
            {
                return BadRequest("AgentId and MachineName are required.");
            }

            _logger.LogInformation("Received heartbeat from agent {AgentId} ({MachineName})", request.AgentId, request.MachineName);

            var resp = await _workerService.SaveHeartbeatAsync(request, HttpContext.RequestAborted);

            return Ok(resp);
        }

        [HttpPost("scanresult")]
        public async Task<IActionResult> UpsertScanResult([FromBody] IEnumerable<ScanResults> results)
        {
            if (results == null)
            {
                return BadRequest("Request body is required.");
            }

            // Basic validation
            foreach (var result in results)
            {
                if (string.IsNullOrWhiteSpace(result.MachineName) || string.IsNullOrWhiteSpace(result.FilePath) || string.IsNullOrWhiteSpace(result.Entity))
                {
                    return BadRequest("MachineName, FilePath and Entity are required.");
                }
            }

            await _workerService.InsertScanResultAsync(results, HttpContext.RequestAborted);

            return Ok(new { Message = "Scan result inserted." });
        }
    }
}
