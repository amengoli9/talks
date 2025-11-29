using KitchenApp.Domain.Models;

namespace KitchenApp.Domain;

public interface IDishRepository
{
   Task<IEnumerable<Dish>> GetAllAsync();
   Task<IEnumerable<Dish>> GetAllNoEFAsync();
   Task<IEnumerable<Dish>> GetAllNoEFErrorAsync();
}
