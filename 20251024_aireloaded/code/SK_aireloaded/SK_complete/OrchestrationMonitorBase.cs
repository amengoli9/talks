using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text;
using System.Text.Json;

public sealed class OrchestrationMonitorBase
{
   protected void WriteResponse(ChatMessageContent response)
   {
      Console.WriteLine($"\n# RESPONSE {response.Role}{(response.AuthorName is not null ? $" - {response.AuthorName}" : string.Empty)}: {response}");
   }

   public ChatHistory History { get; } = [];
   public ValueTask ResponseCallback(ChatMessageContent response)
   {
      this.History.Add(response);
      WriteResponse(response);
      return ValueTask.CompletedTask;
   }



   public ValueTask StreamingResultCallback(StreamingChatMessageContent streamedResponse, bool isFinal)
   {
      string? authorName = null;
      AuthorRole? authorRole = null;
      StringBuilder builder = new();
      //foreach (StreamingChatMessageContent response in streamedResponse)
      //{
      authorName ??= streamedResponse.AuthorName;
      authorRole ??= streamedResponse.Role;

      if (!string.IsNullOrEmpty(streamedResponse.Content))
      {
         builder.Append($"({JsonSerializer.Serialize(streamedResponse.Content)})");
      }
      //}

      //if (builder.Length > 0)
      //{
      //   System.Console.WriteLine($"\n# STREAMED {authorRole ?? AuthorRole.Assistant}{(authorName is not null ? $" - {authorName}" : string.Empty)}: {builder}\n");
      //}



      return ValueTask.CompletedTask;
   }
}