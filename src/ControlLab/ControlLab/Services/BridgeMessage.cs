using System.Text.Json.Serialization;
namespace ControlLab.Services;

public sealed record BridgeMessage
{
    public string Type { get; init; } = "";
    public string? Message { get; init; }
    public string? Id { get; init; }
    public long? Uptime { get; init; }
    public double? Temperature { get; init; }
    public double? Target { get; init; }
    public double? Output { get; init; }
    public double? Kp { get; init; }
    public double? Ki { get; init; }
    public double? Kd { get; init; }
    public bool? Enabled { get; init; }
    public bool? Door { get; init; }
    public string? Fault { get; init; }
}
public sealed record DeviceCommand(string Command, string Id)
{
    public double? Target { get; init; }
    public double? Kp { get; init; }
    public double? Ki { get; init; }
    public double? Kd { get; init; }
    public bool? Enabled { get; init; }
    public double? Duration { get; init; }
}
public sealed record ThermalSample(double Seconds, double Temperature, double Target, double Output, bool Door, bool Enabled);
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(BridgeMessage))]
[JsonSerializable(typeof(DeviceCommand))]
internal partial class ProtocolJsonContext : JsonSerializerContext;
