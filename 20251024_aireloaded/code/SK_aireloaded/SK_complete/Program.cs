using Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Orchestration;
using Microsoft.SemanticKernel.Agents.Orchestration.Sequential;
using Microsoft.SemanticKernel.Agents.Runtime.InProcess;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ModelContextProtocol.Client;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using static System.Net.Mime.MediaTypeNames;


var host = Host.CreateApplicationBuilder(args);
var activitySource = new ActivitySource("AI_DEMO");
# region handle HTTPCLIENT
var handler = new HttpClientHandler();
handler.CheckCertificateRevocationList = false;
var client = new HttpClient(handler);
HttpClient clientfaker = new HttpClient(new FakerHttpMessageHandler());
# endregion

#region OpenTelemetry setup
AppContext.SetSwitch("Microsoft.SemanticKernel.Experimental.GenAI.EnableOTelDiagnosticsSensitive", true);
var resourceBuilder = ResourceBuilder
          .CreateDefault()
          .AddService("AIDay_COMPLETE");

host.Services.AddOpenTelemetry()
      .ConfigureResource(r => r.AddService("SkDemoComplete", serviceVersion: "1.0.0"))
      .WithTracing(builder =>
      {
         builder
         .AddSource("Microsoft.Extensions.AI*")
         .AddSource("Microsoft.SemanticKernel*")
         .AddSource("Experimental.Microsoft.Extensions.AI.*")
         .AddSource("AI_DEMO")
         .AddHttpClientInstrumentation(options =>
         {
            options.EnrichWithHttpRequestMessage = (activity, request) =>
            {
               if (request.Content != null)
               {
                  var body = request.Content.ReadAsStringAsync().Result;
                  activity.SetTag("http.request.body", body);
               }
            };
            options.EnrichWithHttpResponseMessage = (activity, response) =>
            {
               if (response.Content != null)
               {
                  var body = response.Content.ReadAsStringAsync().Result;
                  activity.SetTag("http.response.body", body);
               }
            };
         })
         .AddOtlpExporter();
      })
      .WithMetrics(builder =>
      {
         builder
         .AddMeter("Microsoft.SemanticKernel*")
         .AddMeter("Microsoft.Extensions.AI*")
         .AddMeter("Experimental.Microsoft.Extensions.AI.*")
         .AddOtlpExporter();
      });
host.Logging.ClearProviders();
host.Logging.AddOpenTelemetry(options =>
{
   options.SetResourceBuilder(resourceBuilder);
   options.AddOtlpExporter();
   options.IncludeFormattedMessage = true;
   options.IncludeScopes = true;
});

using var loggerFactory = LoggerFactory.Create(builder =>
{
   builder.AddOpenTelemetry(options =>
   {
      options.SetResourceBuilder(resourceBuilder);
      options.AddOtlpExporter();
      options.IncludeFormattedMessage = true;
      options.IncludeScopes = true;
   });
   builder.SetMinimumLevel(LogLevel.Information);
});

#endregion
var app = host.Build();
var logger = app.Services.GetRequiredService<ILogger<Program>>();
var tracerProvider = app.Services.GetRequiredService<TracerProvider>();

#region configure AI services
var aoai = host.Configuration.GetSection("AzureOpenAI").Get<AzureOpenAIOptions>()
               ?? throw new InvalidOperationException("Sezione 'AzureOpenAI' mancante in configuration.");
if (string.IsNullOrWhiteSpace(aoai.DeploymentName) ||
        string.IsNullOrWhiteSpace(aoai.Endpoint) ||
        string.IsNullOrWhiteSpace(aoai.ApiKey))
{
   throw new InvalidOperationException("Configurazione AzureOpenAI incompleta: DeploymentName/Endpoint/ApiKey.");
}
var modelId = "llama3.1";
var endpoint = "http://localhost:11434/";

#endregion

IKernelBuilder builder = Kernel.CreateBuilder();
builder.AddAzureOpenAIChatCompletion(aoai.DeploymentName!, aoai.Endpoint!, aoai.ApiKey!, httpClient: client, serviceId: "aifoundry");
builder.AddOpenAIChatCompletion("QWEN", "fake-api-key", httpClient: clientfaker, serviceId: "lmstudio");
builder.AddOllamaChatCompletion(modelId, new Uri(endpoint), serviceId: "ollama");

builder.Services.AddSingleton(loggerFactory);

#region chat
//using (var activity = activitySource.StartActivity("INIT_CHAT"))
//{


//   Kernel kernel = builder.Build();
//   ChatCompletionAgent chatCompletionAgent = new()
//   {
//      Name = "chat-agent",
//      Description = "An agent that can chat with the user.",
//      Instruction = "You are a kind agent that help user",
//      Kernel = kernel,

//   };
//   string? userInput;
//   AgentThread thread = new ChatHistoryAgentThread();
//   do
//   {
//      Console.Write("User > ");
//      userInput = Console.ReadLine();
//      if (userInput is null)
//         break;
//      using var userMessageActivity = activitySource.StartActivity("UserMessage", ActivityKind.Internal);
//      userMessageActivity?.SetTag("message.length", userInput.Length);
//      var res = chatCompletionAgent.InvokeAsync(userInput, thread);
//      StringBuilder sb = new StringBuilder();
//      await foreach (var message in res)
//      {
//         sb.Append(message.Message);
//      }
//      Console.WriteLine($"AI > {sb.ToString()}");


//   } while (userInput is not null && userInput.Trim() != "addio") ;

//}
#endregion




#region chat plugin
//using (var activity = activitySource.StartActivity("INIT_CHAT_PLUGIN"))
//{

//   Kernel kernel = builder.Build();

//   kernel.Plugins.AddFromType<TimeInformation>();

//   ChatCompletionAgent chatCompletionAgent = new()
//   {
//      Name = "chat-agent",
//      Description = "An agent that can chat with the user.",
//      Kernel = kernel,
//      Instruction = "You are a travel assistant. Help user to plan their trip.",
//      Arguments = new KernelArguments(new PromptExecutionSettings()
//      {
//         FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
//         ServiceId = "lmstudio"
//      })

//   };
//   string? userInput;
//   AgentThread thread = new ChatHistoryAgentThread();
//   do
//   {
//      Console.Write("User > ");
//      userInput = Console.ReadLine();
//      if (userInput is null)
//         break;
//      using var userMessageActivity = activitySource.StartActivity("UserMessage", ActivityKind.Internal);
//      userMessageActivity?.SetTag("message.length", userInput.Length);
//      var res = chatCompletionAgent.InvokeAsync(userInput, thread);
//      StringBuilder sb = new StringBuilder();
//      await foreach (var message in res)
//      {
//         sb.Append(message.Message);
//      }
//      Console.WriteLine($"AI > {sb.ToString()}");


//   } while (userInput is not null && userInput.Trim() != "addio");

//}
#endregion




#region chat plugin OpenAPI
//using (var activity = activitySource.StartActivity("INIT_CHAT_OPENAPI"))
//{

//   Kernel kernel = builder.Build();
//   await kernel.ImportPluginFromOpenApiAsync(
//      pluginName: "menu",
//      uri: new Uri("http://localhost:5152/swagger/v1/swagger.json")
//   );

//   ChatCompletionAgent chatCompletionAgent = new()
//   {
//      Name = "MenuAgent",
//      Description = "Restaurant agent",
//      Kernel = kernel,
//      Arguments = new KernelArguments(new PromptExecutionSettings()
//      {
//         FunctionChoiceBehavior = FunctionChoiceBehavior.Required(),
//         ServiceId = "aifoundry"
//      }),
//      Instructions = """
//        You are the restaurant's menu assistant and restaurant's maître.
//        Use the menu tools to fetch today's menu.
//        for each dish in the menu tell a summary of the dish including main ingredients, cooking style and flavor profile.
//         Output a compact, structured summary for each dish:
//         {
//           "dishId": "...",
//           "name": "...",
//           "mainIngredients": ["..."],
//           "cookingStyle": "...",
//           "flavorProfile": ["..."]  // e.g., savory, spicy, citrus, creamy, umami
//         }
//        If the user asks for recommendations, suggest dishes based on flavor profiles or dietary preferences.
//        In that case output dishes and a reason for the recommendation
//         Response need to be in italian.
//    """
//   };

//   string? userInput;
//   AgentThread thread = new ChatHistoryAgentThread();
//   do
//   {
//      Console.Write("User > ");
//      userInput = Console.ReadLine();
//      if (userInput is null)
//         break;
//      using var userMessageActivity = activitySource.StartActivity("UserMessage", ActivityKind.Internal);
//      userMessageActivity?.SetTag("message.length", userInput.Length);
//      var res = chatCompletionAgent.InvokeAsync(userInput, thread);
//      StringBuilder sb = new StringBuilder();
//      await foreach (var message in res)
//      {
//         sb.Append(message.Message);
//      }
//      Console.WriteLine($"AI > {sb.ToString()}");


//   } while (userInput is not null && userInput.Trim() != "addio");

//}

#endregion


#region chat plugin mcp
using (var activity = activitySource.StartActivity("INIT_CHAT_MCP"))
{

   await using var mcpClient = await McpClient.CreateAsync(
            new HttpClientTransport(new()
            {
               Name = "FirstMCP",
               Endpoint = new Uri("https://learn.microsoft.com/api/mcp"),
            }));
   //https://learn.microsoft.com/api/mcp
   //Endpoint = new Uri("http://localhost:5245"),


   var tools = await mcpClient.ListToolsAsync();
   builder.Plugins.AddFromFunctions("MyTools", tools.Select(x => x.AsKernelFunction()));
   Kernel kernel = builder.Build();
   ChatCompletionAgent chatCompletionAgent = new()
   {
      Name = "LearnAgent",
      Description = "Microsoft learn agent",
      Kernel = kernel,
      Arguments = new KernelArguments(new PromptExecutionSettings()
      {
         FunctionChoiceBehavior = FunctionChoiceBehavior.Required(),
         ServiceId = "aifoundry"
      }),
      Instructions = """
        You are a c# code assistant. 
        Use Microsoft Learn.
        Be pragmatic in your answers.
    """
   };

   string? userInput;
   AgentThread thread = new ChatHistoryAgentThread();
   do
   {
      Console.Write("User > ");
      userInput = Console.ReadLine();
      if (userInput is null)
         break;
      using var userMessageActivity = activitySource.StartActivity("UserMessage", ActivityKind.Internal);
      userMessageActivity?.SetTag("message.length", userInput.Length);
      var res = chatCompletionAgent.InvokeAsync(userInput, thread);
      StringBuilder sb = new StringBuilder();
      await foreach (var message in res)
      {
         sb.Append(message.Message);
      }
      Console.WriteLine($"AI > {sb.ToString()}");


   } while (userInput is not null && userInput.Trim() != "addio");

}

#endregion






await app.RunAsync();

