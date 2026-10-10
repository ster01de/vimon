using Vimon.Core;
namespace Vimon.Platform.MacOS;

public sealed class MacTelemetry : IPlatformTelemetry
{
    public string PlatformName => "macOS";
    public SystemSnapshot Read() => throw new NotImplementedException(
        "Planned backend: Apple Silicon telemetry, memory pressure and thermal state.");
    public void Dispose() { }
}
