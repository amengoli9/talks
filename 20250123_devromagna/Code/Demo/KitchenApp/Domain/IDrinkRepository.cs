using KitchenApp.Domain;
using KitchenApp.Domain.Models;

namespace KitchenApp.Infrastructure;

public interface IDrinkRepository
{
   Task<IEnumerable<Drink>> GetAllAsync();
   IEnumerable<Drink> GetAll();
   Task<IEnumerable<Drink>> GetAllNoEFAsync();
   Task<IEnumerable<Drink>> GetAllNoEFErrorAsync();
}
