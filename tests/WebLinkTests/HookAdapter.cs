using System.Runtime.InteropServices;
using Reloaded.Hooks;
using Reloaded.Hooks.Definitions;

// The production file is compiled unchanged. Only the Dalamud service boundary is substituted;
// detours and chaining use the actual installed Reloaded backend in this isolated test process.
namespace Dalamud.Plugin.Services
{
    public interface IGameInteropProvider
    {
        Dalamud.Hooking.Hook<T> HookFromSymbol<T>(string moduleName, string exportName, T detour) where T : Delegate;
    }
    internal sealed class TestInterop : IGameInteropProvider
    {
        public bool FailCreation;
        public int Created;
        public Dalamud.Hooking.Hook<T> HookFromSymbol<T>(string moduleName, string exportName, T detour) where T : Delegate
        {
            if (FailCreation) throw new InvalidOperationException("Synthetic failure");
            Created++;
            return new(NativeLibrary.GetExport(NativeLibrary.Load(moduleName), exportName), detour);
        }
    }
}
namespace Dalamud.Hooking
{
    public sealed class Hook<T> : IDisposable where T : Delegate
    {
        private readonly nint address;
        private readonly IHook<T> hook;
        private bool disposed;
        internal Hook(nint address, T detour)
        {
            this.address = address;
            hook = ReloadedHooks.Instance.CreateHook(detour, address.ToInt64());
            hook.Activate(); hook.Disable();
        }
        public T OriginalDisposeSafe => disposed ? Marshal.GetDelegateForFunctionPointer<T>(address) : hook.OriginalFunction;
        public void Enable() => hook.Enable();
        public void Disable() => hook.Disable();
        public void Dispose() { if (disposed) return; hook.Disable(); disposed = true; }
    }
}
