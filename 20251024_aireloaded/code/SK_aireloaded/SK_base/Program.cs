using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading;


var host =  Host.CreateApplicationBuilder(args);
using var activitySource = new System.Diagnostics.ActivitySource("ai_demo");

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
          .AddService("AIDay_BASE");

host.Services.AddOpenTelemetry()
           .ConfigureResource(r => r.AddService("SkDemoBase", serviceVersion: "1.0.0"))
           .WithTracing(builder =>
           {
              builder
              .SetResourceBuilder(resourceBuilder)
              .AddSource("Microsoft.Extensions.AI*")
              .AddSource("Microsoft.SemanticKernel*")
              .AddSource("Experimental.Microsoft.Extensions.AI.*")
              .AddSource("ai_demo")
              .AddHttpClientInstrumentation()
              .AddOtlpExporter();
           })
           .WithMetrics(builder =>
           {
              builder
              .SetResourceBuilder(resourceBuilder)
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
#endregion

#region base
//using (var activity = activitySource.StartActivity("INIT_BASE"))
//{

//   IKernelBuilder builder = Kernel.CreateBuilder();
//builder.AddAzureOpenAIChatCompletion(aoai.DeploymentName!, aoai.Endpoint!, aoai.ApiKey!, httpClient: client);

//   Kernel kernel = builder.Build();
//   var answer = await kernel.InvokePromptAsync("Say hi");
//   Console.WriteLine(answer);
//}
#endregion



#region chat
//using (var activity = activitySource.StartActivity("INIT_CHAT"))
//{

//   IKernelBuilder builder = Kernel.CreateBuilder();
//   builder.AddAzureOpenAIChatCompletion(aoai.DeploymentName!, aoai.Endpoint!, aoai.ApiKey!,httpClient: client );

//   Kernel kernel = builder.Build();

   
//   ChatCompletionAgent chatCompletionAgent = new()
//   {
//      Name = "chat-agent",
//      Description = "An agent that can chat with the user.",
//      Kernel = kernel
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
//      var res =  chatCompletionAgent.InvokeAsync(userInput, thread);
//      StringBuilder sb = new StringBuilder();
//      await foreach (var message in res)
//      {
//         sb.Append(message.Message);
//      }
//      Console.WriteLine($"AI > {sb.ToString()}");


//   } while (userInput is not null && userInput.Trim() != "addio");

//}
#endregion



#region chat plugin
using (var activity = activitySource.StartActivity("INIT_CHAT_PLUGIN"))
{

   IKernelBuilder builder = Kernel.CreateBuilder();
   builder.AddAzureOpenAIChatCompletion(aoai.DeploymentName!, aoai.Endpoint!, aoai.ApiKey!, httpClient: client);

   Kernel kernel = builder.Build();

   kernel.Plugins.AddFromType<TimeInformation>();

   ChatCompletionAgent chatCompletionAgent = new()
   {
      Name = "chat-agent",
      Description = "An agent that can chat with the user.",
      Kernel = kernel,
      Arguments =  new KernelArguments(new AzureOpenAIPromptExecutionSettings() { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() })

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
