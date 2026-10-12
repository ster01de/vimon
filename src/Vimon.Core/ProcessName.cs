namespace Vimon.Core;

public static class ProcessName
{
    /// <summary>
    /// Any program can pick its own name, so control characters are removed: an escape sequence in
    /// a name must not reach the terminal. An empty name becomes <c>pid 123</c>.
    /// </summary>
    public static string Clean(string? name, int pid)
    {
        string cleaned = new string((name ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
        return cleaned.Length > 0 ? cleaned : $"pid {pid}";
    }
}
