using Menu.Utilities;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.Metrics;

namespace MenuApp.Controllers
{
   [ApiController]
   [Route("[controller]")]
   public class WeatherForecastController : ControllerBase
   {
      private static readonly string[] Summaries = new[]
      {
           "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
       };

      private readonly ILogger<WeatherForecastController> _logger;

      public WeatherForecastController(ILogger<WeatherForecastController> logger)
      {
         _logger = logger;
      }

      [HttpGet(Name = "GetWeatherForecast")]
      public IEnumerable<WeatherForecast> Get()
      {
         var toRet = Enumerable.Range(1, 5).Select(index => new WeatherForecast
         {
            Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)]
         })
         .ToArray();

         foreach (var item in toRet)
         {
            _logger.LogInformation("Generated weather forecast: {summary} and {temperature}", item.Summary, item.TemperatureC);
         }
         ApplicationDiagnostics.FreezingDaysCounter.Add(toRet.Count(f => f.TemperatureC < 0));

         return toRet;
      }
   }
}
