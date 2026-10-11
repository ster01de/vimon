namespace Vimon.Core;

public sealed record PcieLink(int? GenerationCurrent, int? WidthCurrent, int? GenerationMax, int? WidthMax)
{
    public static readonly PcieLink Unknown = new(null, null, null, null);
}

public sealed record NvidiaSmiGpu(string Name, PcieLink Pcie);

public static class NvidiaSmi
{
    public const string PcieQueryArguments =
        "--query-gpu=name,pcie.link.gen.current,pcie.link.width.current,pcie.link.gen.max,pcie.link.width.max " +
        "--format=csv,noheader,nounits";

    private const int PcieFieldCount = 4;
    private const string VendorPrefix = "NVIDIA ";

    /// <summary>Parses the output of <see cref="PcieQueryArguments"/>. Malformed lines are skipped.</summary>
    public static IReadOnlyList<NvidiaSmiGpu> ParsePcieQuery(string csv) =>
        csv.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseLine)
            .OfType<NvidiaSmiGpu>()
            .ToList();

    /// <summary>
    /// Returns one PCIe link per entry in <paramref name="gpuNames"/>. The n-th GPU with a given name
    /// gets the n-th nvidia-smi row with the same name, so different models never swap values.
    /// </summary>
    public static IReadOnlyList<PcieLink> MatchByName(
        IReadOnlyList<string> gpuNames,
        IReadOnlyList<NvidiaSmiGpu> smiGpus)
    {
        var rowsByName = smiGpus
            .GroupBy(gpu => NormalizeName(gpu.Name), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        return gpuNames.Select(name =>
        {
            string key = NormalizeName(name);
            int occurrence = seen.GetValueOrDefault(key);
            seen[key] = occurrence + 1;

            return rowsByName.TryGetValue(key, out var rows) && occurrence < rows.Count
                ? rows[occurrence].Pcie
                : PcieLink.Unknown;
        }).ToList();
    }

    private static NvidiaSmiGpu? ParseLine(string line)
    {
        // GPU names could contain commas, so the numeric fields are taken from the right.
        var fields = line.Split(',').Select(field => field.Trim()).ToArray();

        if (fields.Length <= PcieFieldCount)
            return null;

        int nameFieldCount = fields.Length - PcieFieldCount;
        string name = string.Join(",", fields[..nameFieldCount]);

        if (string.IsNullOrWhiteSpace(name))
            return null;

        var pcie = new PcieLink(
            ParseInt(fields[nameFieldCount]),
            ParseInt(fields[nameFieldCount + 1]),
            ParseInt(fields[nameFieldCount + 2]),
            ParseInt(fields[nameFieldCount + 3]));

        return new NvidiaSmiGpu(name, pcie);
    }

    private static string NormalizeName(string name)
    {
        string trimmed = name.Trim();

        return trimmed.StartsWith(VendorPrefix, StringComparison.OrdinalIgnoreCase)
            ? trimmed[VendorPrefix.Length..].Trim()
            : trimmed;
    }

    private static int? ParseInt(string value) =>
        int.TryParse(value, out int number) ? number : null;
}
