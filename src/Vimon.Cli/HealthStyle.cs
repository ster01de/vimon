using Vimon.Core;

namespace Vimon.Cli;

/// <summary>Health levels and their short tokens, shared by every view; colours come from <see cref="Theme"/>.</summary>
internal static class HealthStyle
{
    // Four columns, so findings line up behind the token in themes without colour.
    private const int TokenWidth = 4;

    public static HealthLevel Worst(IReadOnlyList<Diagnostic> diagnostics) =>
        diagnostics.Count == 0 ? HealthLevel.Good : diagnostics.Max(d => d.Level);

    public static string Token(HealthLevel level) => level switch
    {
        HealthLevel.Critical => "CRIT",
        HealthLevel.Warning => "WARN",
        HealthLevel.Info => "INFO",
        _ => "OK"
    };

    public static string PaddedToken(HealthLevel level) => Token(level).PadRight(TokenWidth);

    public static HealthLevel LevelOf(ThermalState state) => state switch
    {
        ThermalState.Critical => HealthLevel.Critical,
        ThermalState.Serious => HealthLevel.Warning,
        _ => HealthLevel.Good
    };

    public static HealthLevel LevelOf(MemoryPressure pressure) => pressure switch
    {
        MemoryPressure.Critical => HealthLevel.Critical,
        MemoryPressure.Warning => HealthLevel.Warning,
        _ => HealthLevel.Good
    };
}
