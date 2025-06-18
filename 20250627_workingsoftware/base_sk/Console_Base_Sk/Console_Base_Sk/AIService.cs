using Console_Base_Sk.Plugin;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace Console_Base_Sk;

class BaseAIService(Kernel kernel, IChatCompletionService chatService) : IAIService
{

   public async Task<string> GetResponseAsync(string prompt, ChatHistory history)
   {
         OpenAIPromptExecutionSettings openAIPromptExecutionSettings = new()
         {
            FunctionChoiceBehavior = FunctionChoiceBehavior.None()
         };

         history.AddUserMessage(prompt);
         ChatOptions options = new ChatOptions();

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

class PluginAIService(Kernel kernel, IChatCompletionService chatService) : IAIService
{

   public async Task<string> GetResponseAsync(string prompt, ChatHistory history)
   {
      OpenAIPromptExecutionSettings openAIPromptExecutionSettings = new()
      {
         FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
      };

      kernel.Plugins.AddFromType<TimePlugin>("Text");
      history.AddUserMessage(prompt);
      ChatOptions options = new ChatOptions();

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


public interface IAIService
{
   Task<string> GetResponseAsync(string prompt, ChatHistory history);
}