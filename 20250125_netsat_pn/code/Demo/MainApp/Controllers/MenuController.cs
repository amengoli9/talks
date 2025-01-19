using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

[ApiController]
[Route("[controller]")]
public class MenuController : ControllerBase
{
   private readonly IHttpClientFactory _httpClientFactory;

   public MenuController(IHttpClientFactory httpClientFactory)
   {
      _httpClientFactory = httpClientFactory;
   }

   [HttpGet]
   public async Task<IActionResult> GetMenu()
   {
      var client = _httpClientFactory.CreateClient();

      var piattiResponse = await client.GetStringAsync("http://localhost:5001/piatti");
      var birreResponse = await client.GetStringAsync("http://localhost:5001/birre");

      return Ok(new
      {
         Piatti = JsonSerializer.Deserialize<List<string>>(piattiResponse),
         Birre = JsonSerializer.Deserialize<List<string>>(birreResponse)
      });
   }
}
