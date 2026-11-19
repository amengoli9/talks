using BabyLog.Distributed.Events;
using RabbitMQ.Client;

namespace BabyLog.Distributed.RabbitMq;

public static class ActivityTopology
{
    public const string Exchange = "babylog.activities";
    public const string Queue = "babylog.summaries";
    public const string DeadLetterQueue = "babylog.summaries.dead";

    public static ConnectionFactory CreateFactory(string uri, string name) => new()
    {
        Uri = new Uri(uri),
        ClientProvidedName = name,
        AutomaticRecoveryEnabled = false
    };

    public static async Task DeclareAsync(IChannel channel, CancellationToken ct)
    {
        await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Direct, durable: true, cancellationToken: ct);
        await channel.QueueDeclareAsync(DeadLetterQueue, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: ct);
        await channel.QueueDeclareAsync(Queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = "",
                ["x-dead-letter-routing-key"] = DeadLetterQueue
            }, cancellationToken: ct);
        await channel.QueueBindAsync(Queue, Exchange, ActivityRecordedV1.RoutingKey, cancellationToken: ct);
        await channel.QueueBindAsync(Queue, Exchange, ActivityDeletedV1.RoutingKey, cancellationToken: ct);
    }
}
