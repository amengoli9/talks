using Console_Base_Sk;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services.AddOpenTelemetry();



var ollamaModelId = "llama3";
var ollamaEndpoint = new Uri("http://localhost:11434");

#pragma warning disable SKEXP0070 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
//var kernelBuilder = Kernel.CreateBuilder().AddOllamaChatCompletion(ollamaModelId, ollamaEndpoint);
builder.Services.AddOllamaChatCompletion(ollamaModelId, ollamaEndpoint);
#pragma warning restore SKEXP0070 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.


builder.Services.AddHostedService<AppService>();
builder.Services.AddKernel();
//builder.Services.AddAzureOpenAIChatCompletion(modelId, endpoint, apiKey, httpClient: client);
builder.Services.AddTransient<IAIService, PluginAIService>();

using var host = builder.Build();

try
{
   await host.RunAsync();
}
catch (Exception ex)
{
   Console.WriteLine($"Errore critico: {ex.Message}");
}

