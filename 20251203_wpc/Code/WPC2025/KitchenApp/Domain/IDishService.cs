using KitchenApp.Domain.Models;

namespace KitchenApp.Domain
{
   public interface IDishService
   {
      Task<IEnumerable<Dish>> GetAllAsync();
   }
}