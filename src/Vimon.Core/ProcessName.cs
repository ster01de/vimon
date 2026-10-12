using System.Globalization;

namespace Vimon.Core;

public static class ProcessName
{
    private const char ReplacementCharacter = '\uFFFD';

    /// <summary>
    /// Any program can pick its own name, so characters that could change the display are removed:
    /// control characters (escape sequences), format characters such as right-to-left overrides,
    /// and line separators. A name cut inside a UTF-8 sequence loses the broken end. An empty name
    /// becomes <c>pid 123</c>.
    /// </summary>
    public static string Clean(string? name, int pid)
    {
        string cleaned = new string((name ?? "").Where(IsShown).ToArray()).TrimEnd(ReplacementCharacter).Trim();
        return cleaned.Length > 0 ? cleaned : $"pid {pid}";
    }

    private static bool IsShown(char c) => char.GetUnicodeCategory(c) is not (
        UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator);
}
