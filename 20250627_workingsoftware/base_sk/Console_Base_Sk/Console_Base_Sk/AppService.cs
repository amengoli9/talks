using Console_Base_Sk.Plugin;
using Menu.Utilities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Ollama;
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
         await DemoSemanticKernelok();
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

   private async Task DemoSemanticKernelok()
   {
      using var activity = ApplicationDiagnostics.ActivitySource.StartActivity("AppService");

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


   private async Task DemoSemanticKernel()
   {
      var builder = Kernel.CreateBuilder();
      var modelId = "llama3.2";
      var endpoint = new Uri("http://localhost:11434");

      builder.Services.AddOllamaChatCompletion(modelId, endpoint);

      builder.Plugins
          .AddFromObject(new TimePlugin());


      Kernel kernel = builder.Build();
      var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

      var settings = new OllamaPromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() };

      Console.WriteLine("""
    Ask questions or give instructions to the copilot such as:
    - Change the alarm to 8
    - What is the current alarm set?
    - Is the light on?
    - Turn the light off please.
    - Set an alarm for 6:00 am.
    """);

      Console.Write("> ");

      string? input = null;
      while ((input = Console.ReadLine()) is not null)
      {
         Console.WriteLine();

         try
         {
            ChatMessageContent chatResult = await chatCompletionService.GetChatMessageContentAsync(input, settings, kernel);
            Console.Write($"\n>>> Result: {chatResult}\n\n> ");
         }
         catch (Exception ex)
         {
            Console.WriteLine($"Error: {ex.Message}\n\n> ");
         }
      }

   }

}
