using KitchenApp.Domain.Models;

namespace KitchenApp.Domain;

public class DrinkService(ILogger<DrinkService> logger, IDrinkRepository repository) : IDrinkService
{
   public async Task<IEnumerable<Drink>> GetAllAsync()
   {
      return await repository.GetAllAsync();
   }

   public IEnumerable<Drink> GetAll()
   {
      return repository.GetAll();
   }

   public async Task<IEnumerable<Drink>> GetAllErrorAsync()
   {
      return await repository.GetAllNoEFErrorAsync();
   }

}
