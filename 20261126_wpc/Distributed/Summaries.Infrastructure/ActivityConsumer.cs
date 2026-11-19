using System.Text.Json;
using BabyLog.Distributed.Events;
using BabyLog.Distributed.RabbitMq;
using BabyLog.Distributed.Summaries.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace BabyLog.Distributed.Summaries.Infrastructure;

internal sealed class ActivityConsumer(
    IServiceScopeFactory scopes, ConnectionFactory factory, ILogger<ActivityConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                if (stoppingToken.IsCancellationRequested)
                    return;

                logger.LogWarning(ex, "Consumo degli eventi sospeso. Nuovo tentativo tra 2 secondi");
            }

            if (!stoppingToken.IsCancellationRequested)
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        var interrupted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        channel.ChannelShutdownAsync += (_, _) =>
        {
            interrupted.TrySetResult();
            return Task.CompletedTask;
        };

        await ActivityTopology.DeclareAsync(channel, stoppingToken);
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: stoppingToken);
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.UnregisteredAsync += (_, _) =>
        {
            interrupted.TrySetResult();
            return Task.CompletedTask;
        };
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, delivery.CancellationToken);
            var ct = cancellation.Token;
            try
            {
                await ProjectAsync(channel, delivery, ct);
            }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested)
                    logger.LogWarning(ex, "Proiezione interrotta. La consegna {EventId} verrà ritentata", delivery.BasicProperties.MessageId);
                interrupted.TrySetResult();
            }
        };

        await channel.BasicConsumeAsync(ActivityTopology.Queue, autoAck: false, consumer, stoppingToken);
        await interrupted.Task.WaitAsync(stoppingToken);
    }

    private async Task ProjectAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<SummaryService>();
            await (delivery.RoutingKey switch
            {
                ActivityRecordedV1.RoutingKey => service.ApplyAsync(Read<ActivityRecordedV1>(delivery.Body.Span), ct),
                ActivityDeletedV1.RoutingKey => service.ApplyAsync(Read<ActivityDeletedV1>(delivery.Body.Span), ct),
                _ => throw new InvalidDataException($"Evento sconosciuto: {delivery.RoutingKey}")
            });
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            await channel.BasicRejectAsync(delivery.DeliveryTag, requeue: false, ct);
            logger.LogWarning(ex, "Evento {EventId} spostato nella dead-letter queue", delivery.BasicProperties.MessageId);
            return;
        }

        await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, ct);
        logger.LogInformation("Proiettato {RoutingKey}, evento {EventId}", delivery.RoutingKey, delivery.BasicProperties.MessageId);
    }

    private static T Read<T>(ReadOnlySpan<byte> body) where T : class =>
        JsonSerializer.Deserialize<T>(body) ?? throw new InvalidDataException("Evento senza contenuto.");
}
