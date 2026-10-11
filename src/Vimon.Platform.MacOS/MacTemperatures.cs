namespace Vimon.Platform.MacOS;

internal enum SmcSensorGroup { Cpu, Gpu }

/// <param name="CpuC">Average over all CPU core sensors.</param>
/// <param name="GpuC">Average over all GPU sensors.</param>
/// <param name="GpuMaxC">Hottest GPU sensor, used as hotspot.</param>
internal sealed record MacTemperatureReading(double? CpuC, double? GpuC, double? GpuMaxC)
{
    public static readonly MacTemperatureReading None = new(null, null, null);
}

internal sealed record SmcSensor(uint Key, SmcKeyInfo Info, SmcSensorGroup Group);

/// <summary>
/// CPU and GPU temperatures from the SMC. Sensor keys differ per chip, so they are discovered once
/// by prefix: Apple Silicon uses Tp*/Te* (performance/efficiency cores) and Tg* (GPU), Intel Macs
/// TC* and TG*.
/// </summary>
internal sealed class MacTemperatures : IDisposable
{
    private const uint MaxKeys = 8192;
    private const double MinValidC = 1;
    private const double MaxValidC = 130;

    private readonly MacSmc _smc;
    private readonly IReadOnlyList<SmcSensor> _sensors;

    private MacTemperatures(MacSmc smc, IReadOnlyList<SmcSensor> sensors)
    {
        _smc = smc;
        _sensors = sensors;
    }

    /// <returns>A reader, or null when the SMC is missing or exposes no known temperature keys.</returns>
    public static MacTemperatures? TryCreate(bool isAppleSilicon)
    {
        var smc = MacSmc.TryOpen();

        if (smc is null)
            return null;

        var sensors = Discover(smc, isAppleSilicon);

        if (sensors.Count > 0)
            return new MacTemperatures(smc, sensors);

        smc.Dispose();
        return null;
    }

    public MacTemperatureReading Read() =>
        Aggregate(_sensors.Select(sensor => (sensor.Group, ReadSensor(sensor))));

    internal static SmcSensorGroup? Classify(string key, bool isAppleSilicon)
    {
        if (isAppleSilicon)
        {
            if (key.StartsWith("Tp", StringComparison.Ordinal) || key.StartsWith("Te", StringComparison.Ordinal))
                return SmcSensorGroup.Cpu;

            return key.StartsWith("Tg", StringComparison.Ordinal) ? SmcSensorGroup.Gpu : null;
        }

        if (key.StartsWith("TC", StringComparison.Ordinal))
            return SmcSensorGroup.Cpu;

        return key.StartsWith("TG", StringComparison.Ordinal) ? SmcSensorGroup.Gpu : null;
    }

    /// <summary>Drops implausible values (unpopulated sensors report 0 or garbage).</summary>
    internal static MacTemperatureReading Aggregate(IEnumerable<(SmcSensorGroup Group, double? ValueC)> samples)
    {
        var valid = samples
            .Where(sample => sample.ValueC is >= MinValidC and <= MaxValidC)
            .Select(sample => (sample.Group, Value: sample.ValueC!.Value))
            .ToList();

        var cpu = valid.Where(sample => sample.Group == SmcSensorGroup.Cpu).Select(sample => sample.Value).ToList();
        var gpu = valid.Where(sample => sample.Group == SmcSensorGroup.Gpu).Select(sample => sample.Value).ToList();

        return new MacTemperatureReading(
            cpu.Count > 0 ? cpu.Average() : null,
            gpu.Count > 0 ? gpu.Average() : null,
            gpu.Count > 0 ? gpu.Max() : null);
    }

    private static List<SmcSensor> Discover(MacSmc smc, bool isAppleSilicon)
    {
        uint count = Math.Min(smc.ReadKeyCount() ?? 0, MaxKeys);
        var sensors = new List<SmcSensor>();

        for (uint index = 0; index < count; index++)
        {
            if (smc.KeyAt(index) is not uint key)
                continue;

            if (Classify(SmcProtocol.FourCcToString(key), isAppleSilicon) is not SmcSensorGroup group)
                continue;

            if (smc.ReadKeyInfo(key) is SmcKeyInfo info && SmcProtocol.IsTemperatureType(info))
                sensors.Add(new SmcSensor(key, info, group));
        }

        return sensors;
    }

    private double? ReadSensor(SmcSensor sensor) =>
        _smc.ReadBytes(sensor.Key, sensor.Info) is byte[] bytes
            ? SmcProtocol.DecodeTemperature(sensor.Info, bytes)
            : null;

    public void Dispose() => _smc.Dispose();
}
