using System.Diagnostics;

namespace Vimon.Core;

/// <summary>
/// Free space of the user's volumes via <see cref="DriveInfo"/>, on macOS and Windows alike. Free
/// space changes slowly, so the volumes are read at most every few seconds. On macOS the value is
/// what <c>df</c> shows; Finder adds purgeable space and therefore shows more.
/// </summary>
public sealed class VolumeReader
{
    private static readonly TimeSpan ReadInterval = TimeSpan.FromSeconds(5);

    private readonly long _start = Stopwatch.GetTimestamp();
    private IReadOnlyList<VolumeSpace> _volumes = [];
    private TimeSpan? _readAt;

    public IReadOnlyList<VolumeSpace> Read()
    {
        var now = Stopwatch.GetElapsedTime(_start);

        if (_readAt is TimeSpan last && now - last < ReadInterval)
            return _volumes;

        _volumes = ReadAll();
        _readAt = now;
        return _volumes;
    }

    private static IReadOnlyList<VolumeSpace> ReadAll()
    {
        bool isWindows = OperatingSystem.IsWindows();
        var volumes = new List<VolumeSpace>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            // The type and mount point come from the mount table; only sizes touch the drive itself.
            bool isFixedOrRemovable = drive.DriveType is DriveType.Fixed or DriveType.Removable;
            string fileSystem = SafeFileSystem(drive);

            if (!VolumeFilter.IsRelevant(drive.Name, fileSystem, isWindows, isFixedOrRemovable))
                continue;

            if (TryRead(drive, fileSystem, isWindows) is VolumeSpace volume)
                volumes.Add(volume);
        }

        return volumes;
    }

    private static string SafeFileSystem(DriveInfo drive)
    {
        try { return drive.DriveFormat; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return ""; }
    }

    /// <returns>Null for a drive that is not ready (empty card reader, ejecting disk) or reports no size.</returns>
    private static VolumeSpace? TryRead(DriveInfo drive, string fileSystem, bool isWindows)
    {
        try
        {
            if (!drive.IsReady || drive.TotalSize <= 0)
                return null;

            string? label = isWindows ? drive.VolumeLabel : null;

            return new VolumeSpace(
                VolumeFilter.NameOf(drive.Name, label, isWindows),
                drive.Name,
                fileSystem,
                (ulong)drive.AvailableFreeSpace,
                (ulong)drive.TotalSize);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
