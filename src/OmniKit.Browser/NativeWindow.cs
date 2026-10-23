using System.Runtime.InteropServices;

namespace OmniKit.Browser;

internal static class NativeWindow
{
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll", SetLastError = true)] internal static extern nint SetParent(nint child, nint parent);
    [DllImport("user32.dll")] internal static extern nint GetParent(nint child);
    [DllImport("user32.dll")] internal static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] internal static extern bool GetClientRect(nint window, out Rect rectangle);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint window, out uint process);

    internal static bool Matches(nint parent, int process)
        => parent != 0 && process > 0 && IsWindow(parent) && GetWindowThreadProcessId(parent, out var actual) != 0 && actual == (uint)process;

    internal static Size ClientSize(nint parent)
        => GetClientRect(parent, out var rectangle) ? new Size(Math.Max(0, rectangle.Right), Math.Max(0, rectangle.Bottom)) : Size.Empty;
}
