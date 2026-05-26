// Wire types per il pattern HITL su AG-UI. AG-UI nativamente non sa cosa sia
// un'approvazione tool, quindi viene tunnellata via una pseudo-tool-call
// "request_approval" con questo payload. Copiati dal sample ufficiale
// Step04_HumanInLoop / Server.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace D13.AgUiBlazor.Server;

public sealed class ApprovalRequest
{
    [JsonPropertyName("approval_id")]
    public required string ApprovalId { get; init; }

    [JsonPropertyName("function_name")]
    public required string FunctionName { get; init; }

    [JsonPropertyName("function_arguments")]
    public IDictionary<string, object?>? FunctionArguments { get; init; }

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

[JsonSerializable(typeof(ApprovalRequest))]
[JsonSerializable(typeof(ApprovalResponse))]
[JsonSerializable(typeof(Dictionary<string, object?>))]
[JsonSerializable(typeof(JsonElement))]
public sealed partial class ApprovalJsonContext : JsonSerializerContext;
