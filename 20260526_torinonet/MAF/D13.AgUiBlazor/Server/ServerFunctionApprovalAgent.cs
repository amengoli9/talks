// Delegating agent che traduce ToolApprovalRequestContent <-> pseudo-tool-call
// "request_approval" per farlo passare sul protocollo AG-UI. Copiato (e
// commentato in italiano) dal sample ufficiale Step04_HumanInLoop / Server.
// Codice di plumbing: non e' il punto della demo, e' il prezzo dell'integrazione.

using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace D13.AgUiBlazor.Server;

internal sealed class ServerFunctionApprovalAgent : DelegatingAIAgent
{
    private readonly JsonSerializerOptions _json;

    public ServerFunctionApprovalAgent(AIAgent innerAgent, JsonSerializerOptions json)
        : base(innerAgent)
    {
        this._json = json;
    }

    protected override Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
        => this.RunCoreStreamingAsync(messages, session, options, cancellationToken)
            .ToAgentResponseAsync(cancellationToken);

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Transforma in ingresso: i FunctionCallContent "request_approval"
        // ricevuti dal client diventano ToolApprovalRequestContent / ToolApprovalResponseContent
        // che l'inner agent capisce.
        var processed = ProcessIncomingFunctionApprovals(messages.ToList(), this._json);

        // Esegue l'inner agent e in uscita transforma i ToolApprovalRequestContent
        // emessi in pseudo-tool-call "request_approval".
        await foreach (var update in this.InnerAgent.RunStreamingAsync(
            processed, session, options, cancellationToken).ConfigureAwait(false))
        {
            yield return ProcessOutgoingApprovalRequests(update, this._json);
        }
    }

#pragma warning disable MEAI001 // Type is experimental

    private static ToolApprovalRequestContent ConvertToolCallToApprovalRequest(
        FunctionCallContent toolCall, JsonSerializerOptions json)
    {
        if (toolCall.Name != "request_approval" || toolCall.Arguments == null)
        {
            throw new InvalidOperationException("Invalid request_approval tool call");
        }

        var request = (toolCall.Arguments.TryGetValue("request", out var reqObj) &&
            reqObj is JsonElement argsElement &&
            argsElement.Deserialize(json.GetTypeInfo(typeof(ApprovalRequest))) is ApprovalRequest approvalRequest &&
            approvalRequest != null ? approvalRequest : null)
            ?? throw new InvalidOperationException("Failed to deserialize approval request from tool call");

        return new ToolApprovalRequestContent(
            requestId: request.ApprovalId,
            new FunctionCallContent(
                callId: request.ApprovalId,
                name: request.FunctionName,
                arguments: request.FunctionArguments));
    }

    private static ToolApprovalResponseContent ConvertToolResultToApprovalResponse(
        FunctionResultContent result, ToolApprovalRequestContent approval, JsonSerializerOptions json)
    {
        var approvalResponse = (result.Result is JsonElement je
            ? (ApprovalResponse?)je.Deserialize(json.GetTypeInfo(typeof(ApprovalResponse)))
            : result.Result is string str
                ? (ApprovalResponse?)JsonSerializer.Deserialize(str, json.GetTypeInfo(typeof(ApprovalResponse)))
                : result.Result as ApprovalResponse)
            ?? throw new InvalidOperationException("Failed to deserialize approval response from tool result");

        return approval.CreateResponse(approvalResponse.Approved);
    }

    private static List<ChatMessage> CopyMessagesUpToIndex(List<ChatMessage> messages, int index)
    {
        var result = new List<ChatMessage>(index);
        for (int i = 0; i < index; i++) { result.Add(messages[i]); }
        return result;
    }

    private static List<AIContent> CopyContentsUpToIndex(IList<AIContent> contents, int index)
    {
        var result = new List<AIContent>(index);
        for (int i = 0; i < index; i++) { result.Add(contents[i]); }
        return result;
    }

    private static List<ChatMessage> ProcessIncomingFunctionApprovals(
        List<ChatMessage> messages, JsonSerializerOptions json)
    {
        List<ChatMessage>? result = null;
        Dictionary<string, ToolApprovalRequestContent> tracked = [];

        for (int mi = 0; mi < messages.Count; mi++)
        {
            var message = messages[mi];
            List<AIContent>? transformed = null;

            for (int j = 0; j < message.Contents.Count; j++)
            {
                var content = message.Contents[j];
                if (content is FunctionCallContent { Name: "request_approval" } toolCall)
                {
                    result ??= CopyMessagesUpToIndex(messages, mi);
                    transformed ??= CopyContentsUpToIndex(message.Contents, j);
                    var req = ConvertToolCallToApprovalRequest(toolCall, json);
                    transformed.Add(req);
                    tracked[toolCall.CallId] = req;
                    result.Add(new ChatMessage(message.Role, transformed)
                    {
                        AuthorName = message.AuthorName,
                        MessageId = message.MessageId,
                        CreatedAt = message.CreatedAt,
                        RawRepresentation = message.RawRepresentation,
                        AdditionalProperties = message.AdditionalProperties,
                    });
                }
                else if (content is FunctionResultContent toolResult &&
                    tracked.TryGetValue(toolResult.CallId, out var approval))
                {
                    result ??= CopyMessagesUpToIndex(messages, mi);
                    transformed ??= CopyContentsUpToIndex(message.Contents, j);
                    transformed.Add(ConvertToolResultToApprovalResponse(toolResult, approval, json));
                    result.Add(new ChatMessage(message.Role, transformed)
                    {
                        AuthorName = message.AuthorName,
                        MessageId = message.MessageId,
                        CreatedAt = message.CreatedAt,
                        RawRepresentation = message.RawRepresentation,
                        AdditionalProperties = message.AdditionalProperties,
                    });
                }
                else
                {
                    result?.Add(message);
                }
            }
        }

        return result ?? messages;
    }

    private static AgentResponseUpdate ProcessOutgoingApprovalRequests(
        AgentResponseUpdate update, JsonSerializerOptions json)
    {
        IList<AIContent>? updatedContents = null;
        for (var i = 0; i < update.Contents.Count; i++)
        {
            var content = update.Contents[i];
            if (content is ToolApprovalRequestContent req && req.ToolCall is FunctionCallContent fc)
            {
                updatedContents ??= [.. update.Contents];
                var approvalData = new ApprovalRequest
                {
                    ApprovalId = req.RequestId,
                    FunctionName = fc.Name,
                    FunctionArguments = fc.Arguments,
                    Message = $"Approve execution of '{fc.Name}'?",
                };

                updatedContents[i] = new FunctionCallContent(
                    callId: req.RequestId,
                    name: "request_approval",
                    arguments: new Dictionary<string, object?> { ["request"] = approvalData });
            }
        }

        if (updatedContents is not null)
        {
            var cru = update.AsChatResponseUpdate();
            return new AgentResponseUpdate(new ChatResponseUpdate
            {
                Role = cru.Role,
                Contents = updatedContents,
                MessageId = cru.MessageId,
                AuthorName = cru.AuthorName,
                CreatedAt = cru.CreatedAt,
                RawRepresentation = cru.RawRepresentation,
                ResponseId = cru.ResponseId,
                AdditionalProperties = cru.AdditionalProperties,
            })
            {
                AgentId = update.AgentId,
                ContinuationToken = update.ContinuationToken,
            };
        }

        return update;
    }

#pragma warning restore MEAI001
}
