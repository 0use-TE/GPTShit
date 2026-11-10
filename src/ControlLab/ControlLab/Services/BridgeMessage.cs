using System.Text.Json.Serialization;
namespace ControlLab.Services;
public sealed record BridgeMessage
{
    public string Type { get; init; } = "";
    public string? Message { get; init; }
    public string? Id { get; init; }
    public bool? Led { get; init; }
    public long? Uptime { get; init; }
}
public sealed record DeviceCommand(string Command, string Id, bool? Value = null);
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(BridgeMessage))]
[JsonSerializable(typeof(DeviceCommand))]
internal partial class ProtocolJsonContext : JsonSerializerContext;
