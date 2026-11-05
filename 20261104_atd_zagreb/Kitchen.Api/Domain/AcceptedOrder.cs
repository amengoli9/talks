namespace Kitchen.Api.Domain;

public sealed class AcceptedOrder
{
    public long Id { get; set; }
    public required string Dish { get; set; }
    public required string DishName { get; set; }
    public DateTimeOffset AcceptedAt { get; set; }
}
