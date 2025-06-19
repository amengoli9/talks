using Console_Base_Sk.Plugin;
using Menu.Utilities;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI.Chat;

namespace Console_Base_Sk;

class BaseAIService(Kernel kernel) : IAIService
{

   public async Task<string> GetResponseAsync(string prompt, ChatHistory history)
   {

      using var activity = ApplicationDiagnostics.ActivitySource.StartActivity("GetResponse");
      OpenAIPromptExecutionSettings openAIPromptExecutionSettings = new()
         {
            FunctionChoiceBehavior = FunctionChoiceBehavior.None()
         };

         
         ChatOptions options = new ChatOptions();
         IChatCompletionService chatService = kernel.GetRequiredService<IChatCompletionService>("azure");
         bool roleWritten = false;
         string fullMessage = string.Empty;
         List<StreamingChatMessageContent> chatUpdates = [];
         await foreach (var chatUpdate in chatService.GetStreamingChatMessageContentsAsync(history,
            executionSettings: openAIPromptExecutionSettings,
      kernel: kernel))
         {
            chatUpdates.Add(chatUpdate);

            Console.Write(chatUpdate);
         }

         Console.WriteLine("\n------------------------");

      return chatUpdates.ToString();
   }

}

class PluginAIService(Kernel kernel) : IAIService
{

   public async Task<string> GetResponseAsyncNo(string prompt, ChatHistory history)
   {
      var settings = new OllamaPromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() };

      var chatService = kernel.GetRequiredService<IChatCompletionService>();
      //kernel.Plugins.AddFromType<TimePlugin>("Text");
      kernel.Plugins
    .AddFromObject(new TimePlugin());
      history.AddUserMessage(prompt);
      ChatOptions options = new ChatOptions();

      bool roleWritten = false;
      string fullMessage = string.Empty;
      List<StreamingChatMessageContent> chatUpdates = [];

      Microsoft.SemanticKernel.ChatMessageContent chatResult = await chatService.GetChatMessageContentAsync(prompt, settings, kernel);

      await foreach (var chatUpdate in chatService.GetStreamingChatMessageContentsAsync(history,
         executionSettings: settings,
   kernel: kernel))
      {
         chatUpdates.Add(chatUpdate);

         Console.Write(chatUpdate);
      }

      Console.WriteLine("\n------------------------");

      return chatUpdates.ToString();
   }
   public async Task<string> GetResponseAsync(string prompt, ChatHistory history)
   {
      using var activity = ApplicationDiagnostics.ActivitySource.StartActivity("GetResponse");
      OpenAIPromptExecutionSettings openAIPromptExecutionSettings = new()
      {
         FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
      };

      var settings = new OllamaPromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() };

      IChatCompletionService chatService = kernel.GetRequiredService<IChatCompletionService>("azure");
      //kernel.Plugins.AddFromType<TimePlugin>("Text");
      kernel.Plugins
    .AddFromObject(new TimePlugin());
      history.AddUserMessage(prompt);
      ChatOptions options = new ChatOptions();

      bool roleWritten = false;
      string fullMessage = string.Empty;
      List<StreamingChatMessageContent> chatUpdates = [];
      await foreach (var chatUpdate in chatService.GetStreamingChatMessageContentsAsync(history,
         executionSettings: settings,
   kernel: kernel))
      {
         chatUpdates.Add(chatUpdate);

         Console.Write(chatUpdate);
      }

      Console.WriteLine("\n------------------------");

      return chatUpdates.ToString();
   }
}


public interface IAIService
{
   Task<string> GetResponseAsync(string prompt, ChatHistory history);
}