namespace Vimon.Platform.MacOS.Tests;

/// <summary>A fact that calls real macOS APIs and is skipped on every other OS.</summary>
public sealed class MacOSFactAttribute : FactAttribute
{
    public MacOSFactAttribute()
    {
        if (!OperatingSystem.IsMacOS())
            Skip = "Requires macOS";
    }
}
