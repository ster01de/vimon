using System.Runtime.InteropServices;
using Vimon.Core;

namespace Vimon.Platform.MacOS;

/// <summary>Reads NSProcessInfo.thermalState, the same signal macOS uses to decide on throttling.</summary>
internal sealed class MacThermalState
{
    private readonly IntPtr _processInfo;
    private readonly IntPtr _thermalStateSelector;

    public MacThermalState()
    {
        // NSProcessInfo lives in Foundation, which a plain .NET process does not load on its own.
        NativeLibrary.Load(MacNative.Foundation);

        IntPtr processInfoClass = MacNative.objc_getClass("NSProcessInfo");

        if (processInfoClass == IntPtr.Zero)
            throw new InvalidOperationException("NSProcessInfo is not available");

        _processInfo = MacNative.objc_msgSend(processInfoClass, MacNative.sel_registerName("processInfo"));
        _thermalStateSelector = MacNative.sel_registerName("thermalState");
    }

    public ThermalState? Read()
    {
        if (_processInfo == IntPtr.Zero)
            return null;

        // NSProcessInfoThermalState: 0 nominal, 1 fair, 2 serious, 3 critical.
        long raw = MacNative.objc_msgSend(_processInfo, _thermalStateSelector).ToInt64();

        return raw is >= (long)ThermalState.Nominal and <= (long)ThermalState.Critical
            ? (ThermalState)raw
            : null;
    }
}
