// Lato client di HITL su AG-UI: vede la pseudo-tool-call "request_approval"
// in arrivo dal server e la transforma in ToolApprovalRequestContent che la UI
// puo' intercettare. In senso opposto traduce le risposte di approvazione
// dell'utente. Copiato (e commentato in italiano) dal sample ufficiale
// Step04_HumanInLoop / Client. Boilerplate stabile.

using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace D13.AgUiBlazor.Client;

internal sealed class ServerFunctionApprovalClientAgent : DelegatingAIAgent
{
    private readonly JsonSerializerOptions _json;

    public ServerFunctionApprovalClientAgent(AIAgent innerAgent, JsonSerializerOptions json)
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
        var processed = ProcessOutgoingServerFunctionApprovals(messages.ToList(), this._json);
        await foreach (var update in this.InnerAgent.RunStreamingAsync(
            processed, session, options, cancellationToken).ConfigureAwait(false))
        {
            yield return ProcessIncomingServerApprovalRequests(update, this._json);
        }
    }

#pragma warning disable MEAI001 // Type is experimental

    private static FunctionResultContent ConvertApprovalResponseToToolResult(
        ToolApprovalResponseContent approvalResponse, JsonSerializerOptions json)
        => new(callId: approvalResponse.RequestId,
            result: JsonSerializer.SerializeToElement(new ApprovalResponse
            {
                ApprovalId = approvalResponse.RequestId,
                Approved = approvalResponse.Approved,
            }, json));

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

    private static List<ChatMessage> ProcessOutgoingServerFunctionApprovals(
        List<ChatMessage> messages, JsonSerializerOptions json)
    {
        List<ChatMessage>? result = null;
        Dictionary<string, ToolApprovalRequestContent> approvalRequests = [];

        for (var mi = 0; mi < messages.Count; mi++)
        {
            var message = messages[mi];
            List<AIContent>? transformed = null;
            HashSet<string> approvalCalls = [];

            for (var ci = 0; ci < message.Contents.Count; ci++)
            {
                var content = message.Contents[ci];

                if (content is ToolApprovalRequestContent req &&
                    req.AdditionalProperties?.TryGetValue("original_function", out var origObj) == true &&
                    origObj is FunctionCallContent original)
                {
                    approvalRequests[req.RequestId] = req;
                    transformed ??= CopyContentsUpToIndex(message.Contents, ci);
                    transformed.Add(original);
                }
                else if (content is ToolApprovalResponseContent resp &&
                    approvalRequests.TryGetValue(resp.RequestId, out var correspondingReq))
                {
                    transformed ??= CopyContentsUpToIndex(message.Contents, ci);
                    transformed.Add(ConvertApprovalResponseToToolResult(resp, json));
                    approvalRequests.Remove(resp.RequestId);
                    correspondingReq.AdditionalProperties?.Remove("original_function");
                }
                else if (content is FunctionCallContent { Name: "request_approval" } approvalCall)
                {
                    transformed ??= CopyContentsUpToIndex(message.Contents, ci);
                    approvalCalls.Add(approvalCall.CallId);
                }
                else if (content is FunctionResultContent functionResult &&
                    approvalCalls.Contains(functionResult.CallId))
                {
                    transformed ??= CopyContentsUpToIndex(message.Contents, ci);
                    approvalCalls.Remove(functionResult.CallId);
                }
                else
                {
                    transformed?.Add(content);
                }
            }

            if (transformed?.Count == 0)
            {
                continue;
            }
            else if (transformed != null)
            {
                var newMessage = new ChatMessage(message.Role, transformed)
                {
                    AuthorName = message.AuthorName,
                    MessageId = message.MessageId,
                    CreatedAt = message.CreatedAt,
                    RawRepresentation = message.RawRepresentation,
                    AdditionalProperties = message.AdditionalProperties,
                };
                result ??= CopyMessagesUpToIndex(messages, mi);
                result.Add(newMessage);
            }
            else
            {
                result?.Add(message);
            }
        }

        return result ?? messages;
    }

    private static AgentResponseUpdate ProcessIncomingServerApprovalRequests(
        AgentResponseUpdate update, JsonSerializerOptions json)
    {
        IList<AIContent>? updatedContents = null;
        for (var i = 0; i < update.Contents.Count; i++)
        {
            var content = update.Contents[i];
            if (content is FunctionCallContent { Name: "request_approval" } request)
            {
                updatedContents ??= [.. update.Contents];

                ApprovalRequest? approvalRequest = null;
                if (request.Arguments?.TryGetValue("request", out var reqObj) == true && reqObj is JsonElement je)
                {
                    approvalRequest = je.Deserialize<ApprovalRequest>(json);
                }

                if (approvalRequest is null)
                {
                    throw new InvalidOperationException("Failed to deserialize approval request.");
                }

                var fnArgs = approvalRequest.FunctionArguments?.Deserialize<Dictionary<string, object?>>(json);

                var approvalReqContent = new ToolApprovalRequestContent(
                    requestId: approvalRequest.ApprovalId,
                    new FunctionCallContent(
                        callId: approvalRequest.ApprovalId,
                        name: approvalRequest.FunctionName,
                        arguments: fnArgs));

                approvalReqContent.AdditionalProperties ??= [];
                approvalReqContent.AdditionalProperties["original_function"] = content;

                updatedContents[i] = approvalReqContent;
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

    public sealed class ApprovalRequest
    {
        [JsonPropertyName("approval_id")]
        public required string ApprovalId { get; init; }

        [JsonPropertyName("function_name")]
        public required string FunctionName { get; init; }

        [JsonPropertyName("function_arguments")]
        public JsonElement? FunctionArguments { get; init; }

        [JsonPropertyName("message")]
        public string? Message { get; init; }
    }

    public sealed class ApprovalResponse
    {
        [JsonPropertyName("approval_id")]
        public required string ApprovalId { get; init; }

        [JsonPropertyName("approved")]
        public required bool Approved { get; init; }
    }
}
