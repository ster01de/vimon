namespace Vimon.Core;

/// <summary>Free space of one mounted volume.</summary>
/// <param name="FileSystem">As the OS names it, e.g. apfs, ntfs, exfat, NTFS.</param>
public sealed record VolumeSpace(string Name, string MountPoint, string FileSystem, ulong FreeBytes, ulong TotalBytes)
{
    public double UsedPct => TotalBytes == 0 ? 0 : (TotalBytes - Math.Min(FreeBytes, TotalBytes)) * 100.0 / TotalBytes;
}

/// <summary>Which mounts count as user volumes. Pure.</summary>
public static class VolumeFilter
{
    public const string StartupDiskName = "Startup disk";

    private const string UnixRoot = "/";
    private const string MacVolumesPrefix = "/Volumes/";
    private const string MacSystemVolumesPrefix = "/Volumes/com.apple.";

    // Network mounts can hang when queried; pseudo file systems have no meaningful free space.
    private static readonly string[] SkippedFileSystems =
        ["autofs", "devfs", "nullfs", "smbfs", "afpfs", "nfs", "webdav", "cifs", "fdesc", "tmpfs"];

    /// <summary>
    /// macOS: the startup disk and everything under /Volumes (external drives, disk images), but not
    /// the hidden system volumes of the APFS container, which share the startup disk's free space.
    /// Windows: fixed and removable drives.
    /// </summary>
    public static bool IsRelevant(string mountPoint, string fileSystem, bool isWindows, bool isFixedOrRemovable)
    {
        if (isWindows)
            return isFixedOrRemovable;

        if (SkippedFileSystems.Contains(fileSystem, StringComparer.OrdinalIgnoreCase))
            return false;

        return mountPoint == UnixRoot
            || (mountPoint.StartsWith(MacVolumesPrefix, StringComparison.Ordinal)
                && !mountPoint.StartsWith(MacSystemVolumesPrefix, StringComparison.Ordinal));
    }

    /// <returns><c>Startup disk</c> for /, the folder name under /Volumes, or a Windows label such as <c>Data (D:)</c>.</returns>
    public static string NameOf(string mountPoint, string? label, bool isWindows)
    {
        if (isWindows)
            return string.IsNullOrWhiteSpace(label) ? mountPoint.TrimEnd('\\') : $"{label} ({mountPoint.TrimEnd('\\')})";

        return mountPoint == UnixRoot ? StartupDiskName : mountPoint.TrimEnd('/').Split('/')[^1];
    }
}
