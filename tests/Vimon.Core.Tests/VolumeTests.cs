using Vimon.Core;

namespace Vimon.Core.Tests;

public class VolumeTests
{
    private const ulong Gb = 1UL << 30;

    private static SystemSnapshot WithVolume(VolumeSpace volume) =>
        new SystemSnapshot(DateTimeOffset.UnixEpoch, null, [], new MemoryMetrics(4, 16, 25, 5, 20), []) { Volumes = [volume] };

    [Fact]
    public void ComputesTheUsedShare()
    {
        Assert.Equal(75, new VolumeSpace("x", "/", "apfs", 25 * Gb, 100 * Gb).UsedPct);
        Assert.Equal(0, new VolumeSpace("x", "/", "apfs", 0, 0).UsedPct);
    }

    [Theory]
    [InlineData("/", "apfs", true)]
    [InlineData("/Volumes/TOSHIBA 1TB", "ntfs", true)]
    [InlineData("/Volumes/Untitled", "exfat", true)]
    [InlineData("/System/Volumes/Data", "apfs", false)]
    [InlineData("/Volumes/com.apple.TimeMachine.localsnapshots", "apfs", false)]
    [InlineData("/Volumes/share", "smbfs", false)]
    [InlineData("/dev", "devfs", false)]
    public void KeepsTheStartupDiskAndMountedVolumesOnMacOS(string mountPoint, string fileSystem, bool expected) =>
        Assert.Equal(expected, VolumeFilter.IsRelevant(mountPoint, fileSystem, isWindows: false, isFixedOrRemovable: true));

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void KeepsFixedAndRemovableDrivesOnWindows(bool isFixedOrRemovable, bool expected) =>
        Assert.Equal(expected, VolumeFilter.IsRelevant(@"C:\", "NTFS", isWindows: true, isFixedOrRemovable));

    [Theory]
    [InlineData("/", null, false, "Startup disk")]
    [InlineData("/Volumes/TOSHIBA 1TB", null, false, "TOSHIBA 1TB")]
    [InlineData(@"D:\", "Data", true, "Data (D:)")]
    [InlineData(@"C:\", "", true, "C:")]
    public void NamesVolumesTheWayUsersKnowThem(string mountPoint, string? label, bool isWindows, string expected) =>
        Assert.Equal(expected, VolumeFilter.NameOf(mountPoint, label, isWindows));

    [Fact]
    public void ReportsANearlyFullStartupDiskAsCritical()
    {
        var full = new VolumeSpace("Startup disk", "/", "apfs", (ulong)(2.7 * Gb), 228 * Gb);

        var diagnostic = Assert.Single(DiagnosticsEngine.Evaluate(WithVolume(full)));

        Assert.Equal(HealthLevel.Critical, diagnostic.Level);
        Assert.Equal("DISK_FULL", diagnostic.Code);
        Assert.Equal($"Startup disk: {2.7:0.0} GB free (99% used)", diagnostic.Message);
    }

    [Fact]
    public void WarnsAboutAVolumeThatIsGettingFull()
    {
        var low = new VolumeSpace("Data", "/Volumes/Data", "apfs", 18 * Gb, 200 * Gb);

        Assert.Equal("DISK_LOW", Assert.Single(DiagnosticsEngine.Evaluate(WithVolume(low))).Code);
    }

    [Fact]
    public void IgnoresAFullPercentageWhenPlentyOfSpaceIsLeft()
    {
        var big = new VolumeSpace("Archive", "/Volumes/Archive", "apfs", 400 * Gb, 8192 * Gb);

        Assert.Equal("HEALTHY", Assert.Single(DiagnosticsEngine.Evaluate(WithVolume(big))).Code);
    }

    [Fact]
    public void CleansEveryNameThatCouldReachTheTerminal()
    {
        const string evil = "USB\u001b]0;pwned\u0007\u001b[2J\u202E";
        var snapshot = new SystemSnapshot(
            DateTimeOffset.UnixEpoch,
            new CpuMetrics(evil, null, null, null, null),
            [new GpuMetrics(0, evil, Vendor.Apple, null, null, null, null, null, null, null, null, null, null, null, null, null, null)],
            new MemoryMetrics(4, 16, 25, 5, 20),
            [new StorageMetrics(evil, null)])
        {
            Network = [new InterfaceRate(evil, null, null)],
            Volumes = [new VolumeSpace(evil, "/Volumes/" + evil, evil, 1, 2)],
            TopProcesses = [new ProcessUsage(7, evil, null, 0)],
            TopDiskProcesses = [new ProcessUsage(8, evil, null, 0)]
        };

        var clean = TerminalText.Clean(snapshot);

        string[] names =
        [
            clean.Cpu!.Name, clean.Gpus[0].Name, clean.Storage[0].Name, clean.Network[0].Name,
            clean.Volumes[0].Name, clean.Volumes[0].MountPoint, clean.Volumes[0].FileSystem, clean.TopProcesses[0].Name,
            clean.TopDiskProcesses[0].Name
        ];
        Assert.All(names, name => Assert.DoesNotContain(name, c => char.IsControl(c) || c == '\u202E'));
        Assert.Equal("USB]0;pwned[2J", clean.Volumes[0].Name);
    }

    [Fact]
    public void ReadsAtLeastOneVolumeOfThisMachine()
    {
        var volumes = new VolumeReader().Read();

        Assert.NotEmpty(volumes);
        Assert.All(volumes, v =>
        {
            Assert.True(v.TotalBytes > 0, v.MountPoint);
            Assert.InRange(v.UsedPct, 0, 100);
        });
    }
}
