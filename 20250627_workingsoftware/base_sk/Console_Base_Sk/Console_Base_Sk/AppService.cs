using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Console_Base_Sk;
public class AppService(IAIService aiService, ILogger<AppService> logger, IHostApplicationLifetime lifetime) : IHostedService
{


   public async Task StartAsync(CancellationToken cancellationToken)
   {
      logger.LogInformation("Applicazione avviata alle {Time}", DateTime.Now);

      try
      {

         Console.WriteLine("\n" + new string('=', 50));
         Console.WriteLine("DEMO SEMANTIC KERNEL");
         Console.WriteLine(new string('=', 50));

         // Avvia interfaccia interattiva
         await DemoSemanticKernel();
      }
      catch (Exception ex)
      {
         logger.LogError(ex, "Errore durante l'esecuzione dell'applicazione");
      }
      finally
      {
         lifetime.StopApplication();
      }
   }

   public Task StopAsync(CancellationToken cancellationToken)
   {
      logger.LogInformation("Applicazione terminata alle {Time}", DateTime.Now);
      return Task.CompletedTask;
   }

   private async Task DemoSemanticKernel()
   {
      string? userInput;
      var history = new ChatHistory();
      do
      {
         Console.Write("\n> ");
         userInput = Console.ReadLine()?.Trim();

         history.AddUserMessage(userInput);
         try
         {
            string response = await aiService.GetResponseAsync(userInput, history);
            history.AddAssistantMessage(response);
         }
         catch (Exception ex)
         {
            logger.LogError(ex, "Errore durante l'elaborazione del comando");
            Console.WriteLine("❌ Errore durante l'elaborazione. Riprova.");
         }
      } while (userInput is not null);
   }


}
