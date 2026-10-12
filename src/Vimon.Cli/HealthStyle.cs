using Spectre.Console;
using Vimon.Core;

namespace Vimon.Cli;

/// <summary>Colours and short tokens for health levels, shared by every view so they always agree.</summary>
internal static class HealthStyle
{
    public static HealthLevel Worst(IReadOnlyList<Diagnostic> diagnostics) =>
        diagnostics.Count == 0 ? HealthLevel.Good : diagnostics.Max(d => d.Level);

    public static string Token(HealthLevel level) => level switch
    {
        HealthLevel.Critical => "CRIT",
        HealthLevel.Warning => "WARN",
        HealthLevel.Info => "INFO",
        _ => "OK"
    };

    public static string ColorName(HealthLevel level) => level switch
    {
        HealthLevel.Critical => "red",
        HealthLevel.Warning => "yellow",
        HealthLevel.Info => "blue",
        _ => "green"
    };

    public static Color ColorOf(HealthLevel level) => level switch
    {
        HealthLevel.Critical => Color.Red,
        HealthLevel.Warning => Color.Yellow,
        HealthLevel.Info => Color.Blue,
        _ => Color.Green
    };

    public static string ThermalColor(ThermalState state) => state switch
    {
        ThermalState.Critical => "red",
        ThermalState.Serious => "yellow",
        _ => "green"
    };

    public static string PressureColor(MemoryPressure pressure) => pressure switch
    {
        MemoryPressure.Critical => "red",
        MemoryPressure.Warning => "yellow",
        _ => "green"
    };
}
