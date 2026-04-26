namespace Bloom.Services;

internal static class InternalActionDispatcher
{
    internal static bool TryDispatch(string action)
    {
        switch (action)
        {
            case "@@screenshot":
                WindowsInteropService.SimulateKeys(0x5B, 0x10, 0x53);
                return true;
            case "@@lock":
                WindowsInteropService.Lock();
                return true;
            case "@@sleep":
                ProcessLauncher.Launch("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0");
                return true;
            case "@@hibernate":
                ProcessLauncher.Launch("shutdown", "/h");
                return true;
            case "@@shutdown":
                ProcessLauncher.Launch("shutdown", "/s /t 0");
                return true;
            case "@@restart":
                ProcessLauncher.Launch("shutdown", "/r /t 0");
                return true;
            case "@@taskmgr":
                ProcessLauncher.Launch("taskmgr");
                return true;
            case "@@settings":
                ProcessLauncher.Launch("ms-settings:");
                return true;
            case "@@explorer":
                ProcessLauncher.Launch("explorer");
                return true;
            case "@@calc":
                ProcessLauncher.Launch("calc");
                return true;
            case "@@controlpanel":
                ProcessLauncher.Launch("control");
                return true;
            case "@@emptybin":
                WindowsInteropService.EmptyRecycleBin();
                return true;
            case "@@notepad":
                ProcessLauncher.Launch("notepad");
                return true;
            case "@@clipboard":
                WindowsInteropService.SimulateKeys(0x5B, 0x56);
                return true;
            case "@@showdesktop":
                WindowsInteropService.SimulateKeys(0x5B, 0x44);
                return true;
            case "@@run":
                WindowsInteropService.SimulateKeys(0x5B, 0x52);
                return true;
            case "@@emoji":
                WindowsInteropService.SimulateKeys(0x5B, 0xBE);
                return true;
            case "@@minimizeall":
                WindowsInteropService.SimulateKeys(0x5B, 0x4D);
                return true;
            case "@@signout":
                ProcessLauncher.Launch("shutdown", "/l");
                return true;
            case "@@magnifier":
                ProcessLauncher.Launch("magnify");
                return true;
            case "@@taskview":
                WindowsInteropService.SimulateKeys(0x5B, 0x09);
                return true;
            case "@@snapleft":
                WindowsInteropService.SimulateKeys(0x5B, 0x25);
                return true;
            case "@@snapright":
                WindowsInteropService.SimulateKeys(0x5B, 0x27);
                return true;
            case "@@maximize":
                WindowsInteropService.SimulateKeys(0x5B, 0x26);
                return true;
            case "@@newdesktop":
                WindowsInteropService.SimulateKeys(0x11, 0x5B, 0x44);
                return true;
            case "@@closedesktop":
                WindowsInteropService.SimulateKeys(0x11, 0x5B, 0x73);
                return true;
            case "@@prevdesktop":
                WindowsInteropService.SimulateKeys(0x11, 0x5B, 0x25);
                return true;
            case "@@nextdesktop":
                WindowsInteropService.SimulateKeys(0x11, 0x5B, 0x27);
                return true;
            default:
                if (action.StartsWith("@@keys:"))
                {
                    WindowsInteropService.SimulateShortcut(action.Substring(7));
                    return true;
                }
                return false;
        }
    }
}
