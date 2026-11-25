using System.Globalization;
using System.Management;
using System.Runtime.Versioning;

namespace Sherlog.Core.Bundles;

/// <summary>Basic facts about a PC that help explain crashes. Nothing here names a person.</summary>
public sealed record SystemSummary(string Windows, string Build, string Cpu, int MemoryGb, IReadOnlyList<GpuInfo> Gpus)
{
    /// <summary>Reads this PC's summary from WMI. Anything that can't be read is left as "unknown".</summary>
    [SupportedOSPlatform("windows")]
    public static SystemSummary ForThisPc()
    {
        var os = First("SELECT Caption, BuildNumber FROM Win32_OperatingSystem");
        var cpu = First("SELECT Name FROM Win32_Processor");
        var computer = First("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
        var bytes = computer is not null && ulong.TryParse(Text(computer, "TotalPhysicalMemory"), out var b) ? b : 0UL;

        return new SystemSummary(
            Windows: Text(os, "Caption") ?? "unknown",
            Build: Text(os, "BuildNumber") ?? "unknown",
            Cpu: Text(cpu, "Name")?.Trim() ?? "unknown",
            MemoryGb: (int)Math.Round(bytes / (1024.0 * 1024 * 1024)),
            Gpus: All("SELECT Name, DriverVersion, DriverDate FROM Win32_VideoController")
                .Select(g => new GpuInfo(Text(g, "Name") ?? "unknown", Text(g, "DriverVersion"), DriverDate(Text(g, "DriverDate"))))
                .ToList());
    }

    [SupportedOSPlatform("windows")]
    private static Dictionary<string, object?>? First(string query) => All(query).FirstOrDefault();

    [SupportedOSPlatform("windows")]
    private static List<Dictionary<string, object?>> All(string query)
    {
        var rows = new List<Dictionary<string, object?>>();
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\cimv2", query);
            using var results = searcher.Get();
            foreach (var row in results)
            {
                using (row)
                {
                    rows.Add(row.Properties.Cast<PropertyData>().ToDictionary(p => p.Name, p => (object?)p.Value));
                }
            }
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            // A summary with "unknown" values is still useful; the crash records matter more.
        }

        return rows;
    }

    private static string? Text(Dictionary<string, object?>? row, string name) =>
        row is not null && row.TryGetValue(name, out var v) && v is not null
            ? Convert.ToString(v, CultureInfo.InvariantCulture)
            : null;

    // WMI dates look like "20261101000000.000000-000"; keep just the day.
    private static string? DriverDate(string? wmi) =>
        wmi is { Length: >= 8 } ? $"{wmi[..4]}-{wmi[4..6]}-{wmi[6..8]}" : null;
}

public sealed record GpuInfo(string Name, string? DriverVersion, string? DriverDate);
