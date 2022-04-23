using AutoscalerApi;
using AutoscalerApi.Services;
using AutoscalerApi.Workers;
using Docker.DotNet;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.custom.json", true);
var appConfig = new AppConfiguration();
builder.Configuration.Bind(appConfig);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IDockerService, DockerService>();
var dockerConfig = !string.IsNullOrWhiteSpace(appConfig.DockerHost) ? new DockerClientConfiguration(new Uri(appConfig.DockerHost)) : new DockerClientConfiguration();

builder.Services.AddSingleton(_ => dockerConfig.CreateClient());
if (!string.IsNullOrWhiteSpace(appConfig.AzureStorage))
{
    builder.Services.AddHostedService<QueueMonitorWorker>();
}

builder.Services.AddSingleton(appConfig);
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();

if (appConfig.UseWebEndpoint)
{
    app.MapControllers();
}

app.Run();
