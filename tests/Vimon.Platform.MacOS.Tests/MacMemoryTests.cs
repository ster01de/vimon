using Vimon.Core;

namespace Vimon.Platform.MacOS.Tests;

public class MacMemoryTests
{
    private const ulong PageSize = 16384;

    [Fact]
    public void CountsAppWiredAndCompressedPagesAsUsed()
    {
        var vm = new VmStatistics64
        {
            InternalPageCount = 1000,
            PurgeableCount = 100,
            WireCount = 200,
            CompressorPageCount = 50,
            ExternalPageCount = 5000,
            FreeCount = 7000
        };

        Assert.Equal((900UL + 200 + 50) * PageSize, MacMemory.UsedBytes(vm, PageSize));
    }

    [Fact]
    public void NeverUnderflowsWhenPurgeableExceedsInternal()
    {
        var vm = new VmStatistics64 { InternalPageCount = 10, PurgeableCount = 20, WireCount = 5 };

        Assert.Equal(5 * PageSize, MacMemory.UsedBytes(vm, PageSize));
    }

    [Theory]
    [InlineData(1, MemoryPressure.Normal)]
    [InlineData(2, MemoryPressure.Warning)]
    [InlineData(4, MemoryPressure.Critical)]
    [InlineData(3, null)]
    [InlineData(null, null)]
    public void MapsKernelPressureLevels(int? level, MemoryPressure? expected) =>
        Assert.Equal(expected, MacMemory.MapPressure(level));
}
