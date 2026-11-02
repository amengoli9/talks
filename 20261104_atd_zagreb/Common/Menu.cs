namespace RestaurantDemo;

public record MenuItem(string Id, string Name, string Description, decimal Price, string Region);
public record OrderRequest(string? Dish);
public record KitchenReply(bool Accepted, string Dish);
public record NotificationReply(bool Accepted);

public static class Menu
{
    public static readonly MenuItem[] Items =
    [
        new("bruschetta", "Bruschetta", "Toasted country bread, ripe tomatoes, fresh basil and extra virgin olive oil.", 6, "Tuscany"),
        new("caprese", "Insalata caprese", "Fresh mozzarella, sun-ripened tomatoes and basil, finished with extra virgin olive oil.", 9, "Capri"),
        new("margherita", "Pizza Margherita", "Tomato, fior di latte mozzarella and fresh basil. The timeless Neapolitan classic.", 8, "Naples"),
        new("carbonara", "Spaghetti alla carbonara", "Spaghetti with guanciale, egg, Pecorino Romano and black pepper.", 13, "Rome"),
        new("lasagne", "Lasagne alla bolognese", "Layers of egg pasta, slow-cooked beef ragù, béchamel and Parmigiano Reggiano.", 14, "Bologna"),
        new("risotto", "Risotto ai funghi", "Creamy Carnaroli rice with mushrooms, butter and Parmigiano Reggiano.", 12, "Northern Italy"),
        new("parmigiana", "Parmigiana di melanzane", "Baked layers of aubergine, tomato sauce, mozzarella, basil and Parmigiano Reggiano.", 12, "Southern Italy"),
        new("tiramisu", "Tiramisù", "Espresso-soaked ladyfingers, mascarpone cream and a generous dusting of cocoa.", 5, "Veneto"),
        new("cannoli", "Cannoli siciliani con ricotta e pistacchio", "Crisp pastry shells filled with sweet ricotta, chocolate chips and pistachios.", 6, "Sicily")
    ];

    public static MenuItem? Find(string? id) => Items.FirstOrDefault(item =>
        string.Equals(item.Id, id?.Trim(), StringComparison.OrdinalIgnoreCase));
}
