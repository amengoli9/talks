using KitchenApp.Domain;
using KitchenApp.Domain.Models;

namespace KitchenApp.Infrastructure;

public interface IDishRepository
{
   Task<IEnumerable<Dish>> GetAllAsync();
   Task<IEnumerable<Dish>> GetAllNoEFAsync();
   Task<IEnumerable<Dish>> GetAllNoEFErrorAsync();
}
