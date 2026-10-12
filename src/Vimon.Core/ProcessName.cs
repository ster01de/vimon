namespace Vimon.Core;

public static class ProcessName
{
    /// <summary>
    /// Any program can pick its own name, so it is cleaned like every other name that reaches the
    /// terminal (see <see cref="TerminalText"/>). An empty name becomes <c>pid 123</c>.
    /// </summary>
    public static string Clean(string? name, int pid)
    {
        string cleaned = TerminalText.Clean(name);
        return cleaned.Length > 0 ? cleaned : $"pid {pid}";
    }
}
