using Vimon.Core;
namespace Vimon.Platform.Linux;

public sealed class LinuxTelemetry : IPlatformTelemetry
{
    public string PlatformName => "Linux";
    public SystemSnapshot Read() => throw new NotImplementedException(
        "Planned backend: /sys/class/hwmon, /proc, nvidia-smi and DRM/sysfs.");
    public void Dispose() { }
}
