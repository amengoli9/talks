using BabyLog.Distributed.RabbitMq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace BabyLog.Distributed.Activities.Infrastructure;

internal sealed class OutboxPublisher(
    IServiceScopeFactory scopes, ConnectionFactory factory, ILogger<OutboxPublisher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await factory.CreateConnectionAsync(stoppingToken);
                var options = new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true);
                await using var channel = await connection.CreateChannelAsync(options, stoppingToken);
                await ActivityTopology.DeclareAsync(channel, stoppingToken);

                while (channel.IsOpen && !stoppingToken.IsCancellationRequested)
                {
                    if (!await PublishBatchAsync(channel, stoppingToken))
                        await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
            }
            catch (Exception ex)
            {
                if (stoppingToken.IsCancellationRequested)
                    return;

                logger.LogWarning(ex, "Invio outbox sospeso. Nuovo tentativo tra 2 secondi");
            }

            if (!stoppingToken.IsCancellationRequested)
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }

    private async Task<bool> PublishBatchAsync(IChannel channel, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ActivitiesDb>();
        var pending = await db.Outbox.OrderBy(message => message.Sequence).Take(20).ToListAsync(ct);

        foreach (var message in pending)
        {
            var properties = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = message.EventId.ToString()
            };
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            await channel.BasicPublishAsync(ActivityTopology.Exchange, message.RoutingKey, mandatory: true,
                properties, message.Body, timeout.Token);

            db.Outbox.Remove(message);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Pubblicato {RoutingKey}, evento {EventId}", message.RoutingKey, message.EventId);
        }

        return pending.Count > 0;
    }
}
