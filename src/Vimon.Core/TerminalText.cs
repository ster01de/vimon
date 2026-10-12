using System.Globalization;

namespace Vimon.Core;

/// <summary>
/// Names reach the terminal from sources other people control: volume labels on a USB stick,
/// renamed network adapters, process names, driver strings. Characters that could change the
/// display are removed before any view sees them: control characters (escape sequences), format
/// characters such as right-to-left overrides, line separators, and a broken UTF-8 tail.
/// </summary>
public static class TerminalText
{
    private const char ReplacementCharacter = '�';

    public static string Clean(string? text) =>
        new string((text ?? "").Where(IsShown).ToArray()).TrimEnd(ReplacementCharacter).Trim();

    /// <summary>The same snapshot with every name cleaned; one place, so new sources cannot be forgotten in a view.</summary>
    public static SystemSnapshot Clean(SystemSnapshot s) => s with
    {
        Cpu = s.Cpu is null ? null : s.Cpu with { Name = Clean(s.Cpu.Name) },
        Gpus = s.Gpus.Select(g => g with { Name = Clean(g.Name) }).ToList(),
        Storage = s.Storage.Select(d => d with { Name = Clean(d.Name) }).ToList(),
        Network = s.Network.Select(n => n with { Name = Clean(n.Name) }).ToList(),
        TopProcesses = s.TopProcesses.Select(p => p with { Name = ProcessName.Clean(p.Name, p.Pid) }).ToList(),
        TopDiskProcesses = s.TopDiskProcesses.Select(p => p with { Name = ProcessName.Clean(p.Name, p.Pid) }).ToList(),
        Volumes = s.Volumes
            .Select(v => v with { Name = Clean(v.Name), MountPoint = Clean(v.MountPoint), FileSystem = Clean(v.FileSystem) })
            .ToList()
    };

    private static bool IsShown(char c) => char.GetUnicodeCategory(c) is not (
        UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator);
}
