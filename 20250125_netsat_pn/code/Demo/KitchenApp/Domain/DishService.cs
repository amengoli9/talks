using KitchenApp.Domain.Models;
using KitchenApp.Infrastructure;

namespace KitchenApp.Domain;

public class DishService(ILogger<DishService> logger, IDishRepository repository) : IDishService
{
   public async Task<IEnumerable<Dish>> GetAllAsync()
   {
      
      return await repository.GetAllAsync();
   }

}
