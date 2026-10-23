using FFXIVClientStructs.FFXIV.Component.GUI;
using OmniKit.Ui;

unsafe
{
    var checks = 0;
    void Check(bool value, string reason) { checks++; if (!value) throw new InvalidOperationException(reason); }
    AtkUnitBase unit = default;
    AtkCollisionNode header = default;
    "SyntheticWindow"u8.CopyTo(unit.Name);
    Check(!CompanionSurfaces.NativeCanContain(null), "A missing addon cannot contain a companion.");
    Check(!CompanionSurfaces.NativeCanContain(&unit), "A headerless native HUD remains an open ledge.");
    unit.WindowHeaderCollisionNode = &header;
    header.Width = 200; header.Height = 28; header.NodeFlags = NodeFlags.Visible;
    Check(CompanionSurfaces.NativeCanContain(&unit), "A visible positive title header retains normal window containment.");
    header.Width = 0;
    Check(!CompanionSurfaces.NativeCanContain(&unit), "A zero-width dummy header cannot create invisible walls.");
    header.Width = 200; header.Height = 0;
    Check(!CompanionSurfaces.NativeCanContain(&unit), "A zero-height dummy header cannot create invisible walls.");
    header.Height = 28; header.NodeFlags = 0;
    Check(!CompanionSurfaces.NativeCanContain(&unit), "An invisible collision header cannot turn a HUD into a container.");
    header.NodeFlags = NodeFlags.Visible;
    header.IsDrawDisabled = true;
    Check(!CompanionSurfaces.NativeCanContain(&unit), "A draw-disabled collision header cannot turn a HUD into a container.");
    header.IsDrawDisabled = false;
    unit.Name.Clear(); "ToDoList"u8.CopyTo(unit.Name);
    Check(!CompanionSurfaces.NativeCanContain(&unit), "The exact quest tracker remains an open strip even with a visible nonempty dummy header.");
    unit.Name.Clear(); "ToDoListOther"u8.CopyTo(unit.Name);
    Check(CompanionSurfaces.NativeCanContain(&unit), "The known-HUD exception is exact, not a broad name-prefix rule.");
    unit.Name.Fill((byte)'x');
    Check(CompanionSurfaces.NativeCanContain(&unit), "A nonterminated fixed-size name is read within its actual span.");
    Console.WriteLine($"{checks} native header classification checks passed against installed structures; synthetic stack data only, no game calls.");
}

namespace OmniKit
{
    internal static class Services
    {
        public static FixtureClient ClientState => throw new InvalidOperationException("No game service access is allowed.");
        public static FixtureConditions Conditions => throw new InvalidOperationException("No game service access is allowed.");
    }
    internal sealed class FixtureClient { public bool IsLoggedIn => false; }
    internal sealed class FixtureConditions
    {
        public bool this[Dalamud.Game.ClientState.Conditions.ConditionFlag flag] => false;
    }
}
