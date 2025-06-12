using Menu.Utilities;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Text.Json;

[ApiController]
[Route("[controller]")]
public class MenuController(ILogger<MenuController> logger, IHttpClientFactory httpClientFactory) : ControllerBase
{

   [HttpGet]
   public async Task<IActionResult> GetMenu()
   {
      ApplicationDiagnostics.PiadinaConsumed.Add(Random.Shared.Next(1, 10));
      string city = "Ancona";
      logger.LogInformation($"Saluti non strutturati da {city}");
      logger.LogInformation("Saluti da {City}",city);
      return await GetMenuSteyByStepAsync(httpClientFactory);
   }

   [HttpGet("error")]
   public async Task<IActionResult> GetMenuError()
   {
      return await GetMenuSteyByStepErrorAsync(httpClientFactory);
   }


   private async Task<IActionResult> GetMenuSteyByStepAsync(IHttpClientFactory httpClientFactory)
   {
      var client = httpClientFactory.CreateClient();

      var dishesTask = await client.GetStringAsync("http://localhost:5678/Kitchen/dishes");
      var drinksTask = await client.GetStringAsync("http://localhost:5678/Kitchen/drinks");
      
      try
      {
         var toRet = new
         {
            Dishes = JsonSerializer.Deserialize<List<object>>(dishesTask),
            Drinks = JsonSerializer.Deserialize<List<object>>(drinksTask)
         };
         return Ok(toRet);
      }
      catch (Exception ex)
      {
         //logger.LogError(ex.Message, ex);
         Activity.Current?.SetStatus(ActivityStatusCode.Error);
         var tags = new ActivityTagsCollection
                    {
                        { "exception.type", ex.GetType().FullName },
                        { "exception.message", ex.Message },
                        { "exception.stacktrace", ex.StackTrace }
                    };

         Activity.Current?.AddEvent(new ActivityEvent(
             name: "exception",
             tags: tags));

         return StatusCode(500);
      }
   }
   private async Task<IActionResult> GetMenuSteyByStepErrorAsync(IHttpClientFactory httpClientFactory)
   {
      var client = httpClientFactory.CreateClient();

      var dishesTask = await client.GetStringAsync("http://localhost:5678/Kitchen/dishes");
      var drinksTask = await client.GetStringAsync("http://localhost:5678/Kitchen/drinkserror");

      try
      {
         var toRet = new
         {
            Dishes = JsonSerializer.Deserialize<List<object>>(dishesTask),
            Drinks = JsonSerializer.Deserialize<List<object>>(drinksTask)
         };
         return Ok(toRet);
      }
      catch (Exception ex)
      {
         Activity.Current?.SetStatus(ActivityStatusCode.Error);
         var tags = new ActivityTagsCollection
                    {
                        { "exception.type", ex.GetType().FullName },
                        { "exception.message", ex.Message },
                        { "exception.stacktrace", ex.StackTrace }
                    };

         Activity.Current?.AddEvent(new ActivityEvent(
             name: "exception",
             tags: tags));

         return StatusCode(500);
      }
   }

}
