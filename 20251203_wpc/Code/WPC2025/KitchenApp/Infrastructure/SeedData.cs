using KitchenApp.Domain.Models;
using KitchenApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KitchenApp.Infrastructure
{
    public class SeedData
    {
        public static void Initialize(WebApplication app)
        {
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<KitchenContext>();
                db.Database.Migrate();

                if (!db.Dishes.Any())
                {
                    db.Dishes.AddRange(
                        new Dish { Name = "Piadina vuota" },
                        new Dish { Name = "Piadina crudo e squaquerone" },
                        new Dish { Name = "Tagliatelle al ragù" },
                        new Dish { Name = "Cappelletti al ragù" },
                        new Dish { Name = "Cappelletti in brodo" }
                    );
                    db.Drinks.AddRange(
                        new Drink { Name = "Birra artigianale IPA" },
                        new Drink { Name = "Birra lager" }
                    );
                    db.SaveChanges();
                }
            }
        }
    }
}
