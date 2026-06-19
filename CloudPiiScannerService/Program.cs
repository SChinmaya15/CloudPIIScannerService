using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// MongoDB configuration (expecting configuration values in appsettings or environment variables)
var mongoConnection = builder.Configuration["Mongo:ConnectionString"] ?? "mongodb://localhost:27017";
var mongoDatabaseName = builder.Configuration["Mongo:Database"] ?? "cloudpii";

var mongoClient = new MongoClient(mongoConnection);
builder.Services.AddSingleton<IMongoClient>(mongoClient);
builder.Services.AddSingleton(sp => mongoClient.GetDatabase(mongoDatabaseName));

// Register worker service
builder.Services.AddScoped<CloudPiiScannerService.Services.IWorkerService, CloudPiiScannerService.Services.WorkerService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
