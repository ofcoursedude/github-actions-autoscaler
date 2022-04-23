﻿using System.Text;
using System.Text.Json;
using AutoscalerApi.Controllers;
using AutoscalerApi.Services;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;

namespace AutoscalerApi.Workers;

public class QueueMonitorWorker : IHostedService
{
    private readonly IDockerService _dockerService;
    private readonly ILogger<QueueMonitorWorker> _logger;
    private readonly string _connectionString;
    private readonly string _queueName;

    public QueueMonitorWorker(AppConfiguration configuration, IDockerService dockerService,
        ILogger<QueueMonitorWorker> logger)
    {
        _dockerService = dockerService;
        _logger = logger;
        _connectionString = configuration.AzureStorage;
        _queueName = configuration.AzureStorageQueue;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var client = new QueueClient(_connectionString, _queueName);

        await client.CreateIfNotExistsAsync();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                QueueMessage message = await client.ReceiveMessageAsync(TimeSpan.FromSeconds(10), cancellationToken);

                if (message != null)
                {
                    _logger.LogInformation("Dequeued message");
                    var decodedMessage = Encoding.UTF8.GetString(Convert.FromBase64String(message.MessageText));
                    var workflow = JsonSerializer.Deserialize<Workflow>(decodedMessage);
                    if (workflow != null)
                    {
                        _logger.LogInformation($"Executing workflow");
                        await _dockerService.ProcessWorkflow(workflow);
                    }

                    await client.DeleteMessageAsync(message.MessageId, message.PopReceipt,
                        cancellationToken);
                }
                else
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error receiving message");
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
    }
}
