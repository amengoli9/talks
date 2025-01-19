using KitchenApp.Domain;
using KitchenApp.Domain.Models;

namespace KitchenApp.Infrastructure;

public interface IDrinkRepository
{
   Task<IEnumerable<Drink>> GetAllAsync();
   Task<IEnumerable<Drink>> GetAllNoEFAsync();
   Task<IEnumerable<Drink>> GetAllNoEFErrorAsync();
}
