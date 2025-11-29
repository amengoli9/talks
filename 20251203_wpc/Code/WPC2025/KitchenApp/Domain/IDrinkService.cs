using KitchenApp.Domain.Models;

namespace KitchenApp.Domain
{
   public interface IDrinkService
   {
      Task<IEnumerable<Drink>> GetAllAsync();
      IEnumerable<Drink> GetAll();
      Task<IEnumerable<Drink>> GetAllErrorAsync();

   }
}