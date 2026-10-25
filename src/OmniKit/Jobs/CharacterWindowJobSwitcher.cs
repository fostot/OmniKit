using Dalamud.Game.Addon.Events;
using Dalamud.Game.Addon.Events.EventDataTypes;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Keys;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace OmniKit.Jobs;

// Native contracts come from the MIT-licensed FFXIVClientStructs and Dalamud SDK.
// No external tweak implementation or class-to-node table is included here.
internal sealed unsafe class CharacterWindowJobSwitcher : IDisposable
{
    private const string AddonName = "CharacterClass";
    private readonly Configuration configuration;
    private readonly JobSwitchService switcher;
    private readonly List<IAddonEventHandle> handles = [];
    private readonly Dictionary<int, bool> enabledOverrides = [];
    private readonly JobSwitcherInputGate input = new();
    private nint attachedAddon;
    private bool listening;
    private bool disposed;
    private string conflictSignature = "";
    private string confirmedConflictSignature = "";
    private string status = "Disabled.";
    private long lastRequestAt = long.MinValue;

    public CharacterWindowJobSwitcher(Configuration configuration, JobSwitchService switcher)
    {
        this.configuration = configuration;
        this.switcher = switcher;
    }

    public string Status => status;
    public string? ConflictWarning { get; private set; }

    public void ConfirmOtherSwitchersDisabled() => confirmedConflictSignature = conflictSignature;

    public bool OpenConflictConfiguration(string internalName)
    {
        var plugin = Services.PluginInterface.InstalledPlugins.FirstOrDefault(p => p.IsLoaded && p.InternalName == internalName);
        if (plugin?.HasConfigUi != true) return false;
        plugin.OpenConfigUi();
        return true;
    }

    // Called on the framework thread by the owning plugin.
    public void Update()
    {
        if (disposed) return;
        var conflicts = Services.PluginInterface.InstalledPlugins
            .Where(p => p.IsLoaded && p.InternalName is "SimpleTweaksPlugin" or "HaselTweaks")
            .Select(p => p.InternalName).Order().ToArray();
        conflictSignature = string.Join(";", conflicts);
        ConflictWarning = conflicts.Length == 0 ? null :
            "Simple Tweaks / HaselTweaks is loaded. Disable its Character Window Job Switcher / Character Class Switcher tweak before confirming below. OmniKit cannot inspect those tweak settings.";
        var permitted = configuration.CharacterJobSwitcherEnabled &&
            (conflicts.Length == 0 || confirmedConflictSignature == conflictSignature);
        if (!permitted)
        {
            StopListening();
            status = configuration.CharacterJobSwitcherEnabled ? "Waiting for confirmation that the other job switcher tweaks are disabled." : "Disabled.";
            return;
        }
        if (!listening)
        {
            Services.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, AddonName, OnSetup);
            Services.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, AddonName, OnFinalize);
            Services.AddonLifecycle.RegisterListener(AddonEvent.PreReceiveEvent, AddonName, OnReceive);
            Services.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, AddonName, OnRequestedUpdate);
            listening = true;
        }
        if (attachedAddon == 0)
        {
            var addon = Services.GameGui.GetAddonByName(AddonName, 1);
            if (!addon.IsNull) Attach(addon.Address);
        }
    }

    private void OnSetup(AddonEvent _, AddonArgs args) => Attach(args.Addon.Address);

    private void OnFinalize(AddonEvent _, AddonArgs args)
    {
        if (args.Addon.Address == attachedAddon) Detach();
    }

    private void Attach(nint address)
    {
        if (address == attachedAddon || !listening || address == 0) return;
        Detach();
        var addon = (AddonCharacterClass*)address;
        // Fail closed if a future SDK changes this particular native contract.
        if (sizeof(AddonCharacterClass) != 0x8D8 || addon->ClassComponents.Length != 34 || addon->ClassEntries.Length != 34)
        {
            status = "Character window layout is unsupported; job switching is inactive.";
            return;
        }
        attachedAddon = address;
        try
        {
            for (var index = 0; index < addon->ClassComponents.Length; index++)
            {
                var component = addon->ClassComponents[index].Value;
                if (component == null || component->OwnerNode == null) continue;
                if (component->GetComponentType() == ComponentType.Button) continue;
                var node = (nint)component->OwnerNode;
                var capturedIndex = index;
                Add(node, AddonEventType.MouseClick, capturedIndex);
                Add(node, AddonEventType.InputReceived, capturedIndex);
            }
            EnableCraftButtons();
            status = "Ready. Click a class/job in Character → Classes/Jobs. Shift-click crafters opens desynthesis.";
        }
        catch (Exception exception)
        {
            Detach();
            status = "Could not attach to the Character window.";
            Services.Log.Error(exception, "Character job switcher attachment failed.");
        }
    }

    private void Add(nint node, AddonEventType type, int index)
    {
        var handle = Services.AddonEventManager.AddEvent(attachedAddon, node, type,
            (eventType, data) => OnOwnedEvent(index, eventType, data));
        if (handle == null) throw new InvalidOperationException("Character class event registration was rejected.");
        handles.Add(handle);
    }

    private void OnOwnedEvent(int index, AddonEventType type, AddonEventData data)
    {
        if (!listening || data.AddonPointer != attachedAddon || !TryGetJob(index, out var job, out _)) return;
        if (type == AddonEventType.MouseClick && JobSwitcherMouseInput.IsLeftClick((AtkEventData*)data.AtkEventDataPointer)) Request(job);
        else if (type == AddonEventType.InputReceived && data.AtkEventDataPointer != 0)
        {
            var eventData = (AtkEventData*)data.AtkEventDataPointer;
            if (input.Accept(index, eventData->InputData.InputId, eventData->InputData.State)) Request(job);
        }
    }

    private void OnReceive(AddonEvent _, AddonArgs arguments)
    {
        if (arguments is not AddonReceiveEventArgs args || args.Addon.Address != attachedAddon) return;
        var index = args.EventParam - 2;
        if (!TryGetJob(index, out var job, out var isCraft) || !isCraft) return;
        if (Services.Keys[VirtualKey.SHIFT]) { input.Reset(); return; }
        var type = args.AtkEventType;
        if (type is not (AddonEventType.ButtonClick or AddonEventType.MouseClick or AddonEventType.InputReceived)) return;
        if (args.AtkEvent == 0 || ((AtkEvent*)args.AtkEvent)->Listener != (AtkEventListener*)attachedAddon) return;
        if (type is AddonEventType.ButtonClick or AddonEventType.MouseClick &&
            !JobSwitcherMouseInput.IsLeftClick((AtkEventData*)args.AtkEventData)) return;
        var accepted = type is AddonEventType.ButtonClick or AddonEventType.MouseClick;
        if (type == AddonEventType.InputReceived)
        {
            if (args.AtkEventData == 0) return;
            var data = (AtkEventData*)args.AtkEventData;
            if (data->InputData.InputId != 1) return;
            accepted = input.Accept(index, data->InputData.InputId, data->InputData.State);
        }
        // Reserve this eligible crafter activation for OmniKit. Shift and all
        // other native events retain their original event type and parameter.
        args.AtkEventType = 0;
        args.EventParam = -1;
        if (accepted) Request(job);
    }

    private bool TryGetJob(int index, out uint job, out bool isCraft)
    {
        job = 0;
        isCraft = false;
        if (attachedAddon == 0) return false;
        var addon = (AddonCharacterClass*)attachedAddon;
        if ((uint)index >= (uint)addon->ClassComponents.Length || addon->ClassEntries[index].Level == 0) return false;
        var component = addon->ClassComponents[index].Value;
        if (component == null) return false;
        isCraft = component->GetComponentType() == ComponentType.Button;
        var image = component->GetImageNodeById(isCraft ? 6u : 4u);
        if (image == null || image->PartsList == null || image->PartsList->Parts == null || image->PartId >= image->PartsList->PartCount) return false;
        var asset = image->PartsList->Parts[image->PartId].UldAsset;
        if (asset == null || asset->AtkTexture.Resource == null) return false;
        var icon = asset->AtkTexture.Resource->IconId;
        if (icon <= 62100 || icon > 62199) return false;
        job = icon - 62100;
        return true;
    }

    private void Request(uint job)
    {
        if (!configuration.CharacterJobSwitcherEnabled || switcher.Busy) return;
        var now = Environment.TickCount64;
        if (lastRequestAt != long.MinValue && now - lastRequestAt < 250) return;
        lastRequestAt = now;
        switcher.TryRequest(job);
    }

    private void OnRequestedUpdate(AddonEvent _, AddonArgs args)
    {
        if (args.Addon.Address == attachedAddon) EnableCraftButtons();
    }

    private void EnableCraftButtons()
    {
        if (attachedAddon == 0) return;
        var addon = (AddonCharacterClass*)attachedAddon;
        for (var index = 0; index < addon->ClassComponents.Length; index++)
        {
            var component = addon->ClassComponents[index].Value;
            if (component == null || component->GetComponentType() != ComponentType.Button || addon->ClassEntries[index].Level == 0) continue;
            if ((component->ComponentFlags & 2) != 0) continue;
            enabledOverrides.TryAdd(index, false);
            component->SetEnabledState(true);
        }
    }

    private void Detach()
    {
        foreach (var handle in handles) Services.AddonEventManager.RemoveEvent(handle);
        handles.Clear();
        if (attachedAddon != 0)
        {
            var addon = (AddonCharacterClass*)attachedAddon;
            foreach (var (index, enabled) in enabledOverrides)
            {
                var component = addon->ClassComponents[index].Value;
                if (component != null && (component->ComponentFlags & 2) != 0) component->SetEnabledState(enabled);
            }
        }
        enabledOverrides.Clear();
        attachedAddon = 0;
        input.Reset();
    }

    private void StopListening()
    {
        if (!listening) return;
        Detach();
        Services.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, AddonName, OnSetup);
        Services.AddonLifecycle.UnregisterListener(AddonEvent.PreFinalize, AddonName, OnFinalize);
        Services.AddonLifecycle.UnregisterListener(AddonEvent.PreReceiveEvent, AddonName, OnReceive);
        Services.AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, AddonName, OnRequestedUpdate);
        listening = false;
    }

    public void Dispose()
    {
        if (disposed) return;
        StopListening();
        disposed = true;
    }
}

internal sealed class JobSwitcherInputGate
{
    private int pressedIndex = -1;
    private bool invalidated;
    public bool Accept(int index, int inputId, InputState state)
    {
        if (inputId != 1)
        {
            if (pressedIndex != -1) invalidated = true;
            return false;
        }
        if (state == InputState.Down)
        {
            pressedIndex = index;
            invalidated = false;
            return false;
        }
        if (state == InputState.Held)
        {
            if (pressedIndex == -1 && !invalidated) pressedIndex = index;
            else if (pressedIndex != index) invalidated = true;
            return false;
        }
        var accepted = state == InputState.Up && !invalidated && pressedIndex == index;
        Reset();
        return accepted;
    }
    public void Reset() { pressedIndex = -1; invalidated = false; }
}

internal static unsafe class JobSwitcherMouseInput
{
    // Dalamud's public AddonMouseEventData.IsLeftClick uses this same native
    // field contract: ButtonId 0 is left; 1 is right. A missing payload fails closed.
    public static bool IsLeftClick(AtkEventData* data) => data != null && data->MouseData.ButtonId == 0;
}
