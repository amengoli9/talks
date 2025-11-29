using Microsoft.AspNetCore.Mvc;
using KitchenApp.Domain;
using KitchenApp.Infrastructure.Data;
using System.Text.Json;

[ApiController]
[Route("[controller]")]
public class KitchenController(ILogger<KitchenController> logger, IDishService dishService, IDrinkService drinkService) : ControllerBase
{


   [HttpGet("dishes")]
   public async Task<IActionResult> GetDishesAsync()
   {
      var dishes = await dishService.GetAllAsync();
      return Ok(dishes);
   }



   [HttpGet("drinks")]
   public async Task<IActionResult> GetDrinksAsync()
   {
      var drinks = await drinkService.GetAllAsync();
      return Ok(drinks);
   }


   [HttpGet("drinksError")]
   public async Task<IActionResult> GetDrinksErrorAsync()
   {
      var drinks = await drinkService.GetAllErrorAsync();
      return Ok(drinks);
   }


   [HttpGet("drinksSync")]
   public IActionResult GetDrinks()
   {
      var drinks = drinkService.GetAll();
      return Ok(drinks);
   }
}