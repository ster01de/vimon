using Vimon.Core;
namespace Vimon.Platform.Linux;

public sealed class LinuxTelemetry : IPlatformTelemetry
{
    public LinuxTelemetry() => throw new PlatformNotSupportedException(
        "The Linux backend is not implemented yet (planned: /sys/class/hwmon, /proc, nvidia-smi and DRM/sysfs).");

    public string PlatformName => "Linux";
    public SystemSnapshot Read() => throw new NotImplementedException();
    public void Dispose() { }
}
