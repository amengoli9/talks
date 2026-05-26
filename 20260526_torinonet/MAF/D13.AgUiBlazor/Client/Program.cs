// D13 Client - Blazor Server. La UI parla con il server AG-UI via AGUIChatClient
// (implementa IChatClient) wrappato in un AIAgent, a sua volta wrappato in
// ServerFunctionApprovalClientAgent per gestire HITL.

using System.Text.Json;
using D13.AgUiBlazor.Client;
using D13.AgUiBlazor.Client.Components;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.AGUI;
using Microsoft.Extensions.AI;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

string serverUrl = builder.Configuration["AGUI_SERVER_URL"] ?? "http://localhost:5100";

builder.Services.AddHttpClient("aguiserver", c =>
{
    c.BaseAddress = new Uri(serverUrl);
    c.Timeout = TimeSpan.FromMinutes(2);
});

// L'agente "remoto": dietro le quinte un POST + SSE verso il server AG-UI.
// Wrappato in ServerFunctionApprovalClientAgent per HITL.
builder.Services.AddScoped<AIAgent>(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("aguiserver");
    AGUIChatClient chatClient = new(httpClient, "ag-ui");
    AIAgent baseAgent = chatClient.AsAIAgent(
        name: "TravelAssistant",
        instructions: "You are a helpful travel assistant.");
    return new ServerFunctionApprovalClientAgent(baseAgent, JsonSerializerOptions.Default);
});

WebApplication app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
