using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Xunit;

public sealed class HumanInTheLoopTests
{
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public async Task SingleDecisionDoesNotAuthorizeTheNextCall(bool approved, int expectedCount)
    {
        using var client = new AnnouncementClient();
        var demo = new AnnouncementDemo();
        var agent = demo.CreateAgent(client);
        var session = await agent.CreateSessionAsync();
        var request = Pending(await agent.RunAsync("Pubblica.", session));
        Assert.Equal(0, demo.PublicationCount);

        await Reply(agent, session, request.CreateResponse(approved));
        Assert.Equal(expectedCount, demo.PublicationCount);
        Pending(await agent.RunAsync("Pubblica ancora.", session));
        Assert.Equal(expectedCount, demo.PublicationCount);
    }

    [Theory]
    [InlineData("evento", "Benvenuti!", true)]
    [InlineData("evento", "Testo cambiato!", false)]
    [InlineData("bozze", "Benvenuti!", false)]
    public async Task RememberedArgumentsMatchTheEntireCall(string channel, string message, bool automatic)
    {
        using var client = new AnnouncementClient();
        var demo = new AnnouncementDemo();
        var agent = demo.CreateAgent(client);
        var session = await agent.CreateSessionAsync();
        var request = Pending(await agent.RunAsync("Pubblica.", session));
        await Reply(agent, session, request.CreateAlwaysApproveToolWithArgumentsResponse());
        Assert.Equal(1, demo.PublicationCount);

        client.Channel = channel;
        client.Message = message;
        var response = await agent.RunAsync("Pubblica ancora.", session);
        Assert.Equal(automatic ? 2 : 1, demo.PublicationCount);
        Assert.Equal(!automatic, HasApproval(response));
    }

    [Fact]
    public async Task RememberedToolAllowsChangedArgumentsButDoesNotCrossSessions()
    {
        using var client = new AnnouncementClient { Channel = "bozze" };
        var demo = new AnnouncementDemo();
        var agent = demo.CreateAgent(client); // Nessuna regola automatica.
        var session = await agent.CreateSessionAsync();
        var request = Pending(await agent.RunAsync("Pubblica.", session));
        await Reply(agent, session, request.CreateAlwaysApproveToolResponse());

        client.Channel = "evento";
        client.Message = "Testo cambiato!";
        Assert.False(HasApproval(await agent.RunAsync("Pubblica ancora.", session)));
        Assert.Equal(2, demo.PublicationCount);

        var newSession = await agent.CreateSessionAsync();
        Pending(await agent.RunAsync("Pubblica.", newSession));
        Assert.Equal(2, demo.PublicationCount);
    }

    [Theory]
    [InlineData(false, "bozze", false)]
    [InlineData(true, "bozze", true)]
    [InlineData(true, "evento", false)]
    public async Task AutoRuleIsOptInAndFalseStillAllowsAHumanDecision(bool enabled, string channel, bool automatic)
    {
        using var client = new AnnouncementClient { Channel = channel };
        var demo = new AnnouncementDemo();
        var agent = demo.CreateAgent(client, enabled);
        var session = await agent.CreateSessionAsync();
        var response = await agent.RunAsync("Pubblica.", session);

        Assert.Equal(automatic ? 1 : 0, demo.PublicationCount);
        Assert.Equal(!automatic, HasApproval(response));
        if (!automatic)
        {
            await Reply(agent, session, Pending(response).CreateResponse(true));
            Assert.Equal(1, demo.PublicationCount); // false nella regola non è un divieto.
        }
    }

    private static bool HasApproval(AgentResponse response) =>
        response.Messages.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>().Any();

    private static ToolApprovalRequestContent Pending(AgentResponse response) =>
        Assert.Single(response.Messages.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>());

    private static Task<AgentResponse> Reply(AIAgent agent, AgentSession session, AIContent answer) =>
        agent.RunAsync([new ChatMessage(ChatRole.User, [answer])], session);

    // Solo il modello è simulato: agente, wrapper, regola e tool sono quelli reali di D8.
    private sealed class AnnouncementClient : IChatClient
    {
        private int calls;
        public string Channel { get; set; } = "evento";
        public string Message { get; set; } = "Benvenuti!";
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            calls++;
            return Task.FromResult(calls % 2 == 1
                ? new ChatResponse(new ChatMessage(ChatRole.Assistant,
                    [new FunctionCallContent($"call-{calls}", "PublishAnnouncement",
                        new Dictionary<string, object?> { ["channel"] = Channel, ["message"] = Message })]))
                : new ChatResponse(new ChatMessage(ChatRole.Assistant, "Fine.")));
        }
        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceType == typeof(ChatClientMetadata) ? new ChatClientMetadata("scripted-hitl-test") : null;
        public void Dispose() { }
    }
}
