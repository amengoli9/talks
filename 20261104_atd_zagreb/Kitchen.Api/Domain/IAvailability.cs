namespace Kitchen.Api.Domain;

public interface IAvailability
{
    Task<bool> IsAvailableAsync(string dish, CancellationToken cancellationToken);
}
