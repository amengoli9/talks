using KitchenApp.Domain.Models;
using KitchenApp.Infrastructure;

namespace KitchenApp.Domain;

public class DrinkService(ILogger<DrinkService> logger, IDrinkRepository repository) : IDrinkService
{
   public async Task<IEnumerable<Drink>> GetAllAsync()
   {
      return await repository.GetAllAsync();
   }

}
