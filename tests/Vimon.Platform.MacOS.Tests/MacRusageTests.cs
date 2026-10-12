using System.Buffers.Binary;
using System.Diagnostics;
using Vimon.Core;

namespace Vimon.Platform.MacOS.Tests;

public class MacRusageTests
{
    private const int WriteBytes = 8 * 1024 * 1024;

    [Fact]
    public void ReadsDiskBytesAtTheirOffsets()
    {
        var info = new byte[MacRusage.Length];
        BinaryPrimitives.WriteUInt64LittleEndian(info.AsSpan(144), 1_000);
        BinaryPrimitives.WriteUInt64LittleEndian(info.AsSpan(152), 33_554_432);

        Assert.Equal((1_000UL, 33_554_432UL), MacRusage.Parse(info));
    }

    [MacOSFact]
    public void SeesItsOwnDiskWrites()
    {
        var reader = new MacProcesses();
        var clock = Stopwatch.StartNew();
        var (_, tracker) = ProcessCpuTracker.Empty.Advance(reader.ReadAll(), clock.Elapsed, Environment.ProcessorCount);

        string path = Path.GetTempFileName();
        try
        {
            using (var stream = new FileStream(path, FileMode.Create))
            {
                stream.Write(new byte[WriteBytes]);
                stream.Flush(flushToDisk: true);
            }

            var (usages, _) = tracker.Advance(reader.ReadAll(), clock.Elapsed, Environment.ProcessorCount);

            var self = Assert.Single(usages, u => u.Pid == Environment.ProcessId);
            Assert.True(self.DiskWriteBytesPerSec > 0, $"write rate {self.DiskWriteBytesPerSec}");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
