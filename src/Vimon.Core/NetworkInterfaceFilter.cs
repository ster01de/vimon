namespace Vimon.Core;

/// <summary>
/// Keeps physical links and drops virtual ones. Tunnels matter most: a VPN sends the same bytes
/// again through Wi-Fi or Ethernet, so counting both would double the traffic.
/// </summary>
public static class NetworkInterfaceFilter
{
    // utun: VPN tunnels; awdl/llw: AirDrop and peer-to-peer Wi-Fi; ap: Personal Hotspot;
    // anpi/bridge: internal links to Thunderbolt and VMs; gif/stf/ipsec: IPv6 and IPsec tunnels.
    private static readonly string[] MacVirtualPrefixes =
        ["utun", "awdl", "llw", "ap", "anpi", "bridge", "gif", "stf", "ipsec"];

    private static readonly string[] WindowsVirtualMarkers =
        ["Hyper-V", "VirtualBox", "VMware", "WAN Miniport", "Npcap", "TAP-", "Wintun", "WireGuard",
         "Teredo", "isatap", "Bluetooth", "Loopback"];

    public static bool IsMacPhysicalName(string name) =>
        name.Length > 0 && !MacVirtualPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal));

    public static bool IsWindowsVirtualDescription(string description) =>
        WindowsVirtualMarkers.Any(marker => description.Contains(marker, StringComparison.OrdinalIgnoreCase));
}
