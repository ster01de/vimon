using Vimon.Core;
namespace Vimon.Platform.MacOS;

public sealed class MacTelemetry : IPlatformTelemetry
{
    public MacTelemetry() => throw new PlatformNotSupportedException(
        "The macOS backend is not implemented yet (planned: Apple Silicon telemetry, memory pressure and thermal state).");

    public string PlatformName => "macOS";
    public SystemSnapshot Read() => throw new NotImplementedException();
    public void Dispose() { }
}
