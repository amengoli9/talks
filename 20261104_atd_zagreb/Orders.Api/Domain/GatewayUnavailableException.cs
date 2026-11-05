namespace Orders.Api.Domain;

// Outbound adapters translate transport failures into this application failure.
public sealed class GatewayUnavailableException(string message, bool timedOut, Exception innerException)
    : Exception(message, innerException)
{
    public bool TimedOut { get; } = timedOut;
}
