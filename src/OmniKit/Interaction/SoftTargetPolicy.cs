namespace OmniKit.Interaction;

internal enum SoftTargetAction { None, UseObject, TargetEnemy, AttackEnemy }

/// <summary>Separates visible candidates from the actions the user has enabled.</summary>
internal static class SoftTargetPolicy
{
    /// <summary>One synchronous selection, confirmation, then action; no retries.</summary>
    public static bool AfterConfirmedTarget(Func<bool> select, Func<bool> confirm, Func<bool> action) =>
        select() && confirm() && action();

    public static bool IsEnemy(InteractionTargets kind) =>
        kind is InteractionTargets.HostileEnemies or InteractionTargets.NeutralEnemies;

    public static bool IsEnabled(InteractionTargets enabled, InteractionTargets kind)
    {
        var value = (int)kind;
        return (enabled & ~InteractionTargets.All) == 0 && value > 0 && (value & (value - 1)) == 0
            && (kind & ~InteractionTargets.All) == 0 && (enabled & kind) != 0;
    }

    public static SoftTargetAction ActionFor(InteractionTargets enabled, EnemyInteractMode mode, InteractionTargets kind)
    {
        if (!IsEnabled(enabled, kind)) return SoftTargetAction.None;
        if (!IsEnemy(kind)) return SoftTargetAction.UseObject;
        return mode switch
        {
            EnemyInteractMode.TargetOnly => SoftTargetAction.TargetEnemy,
            EnemyInteractMode.StartAttack => SoftTargetAction.AttackEnemy,
            _ => SoftTargetAction.None,
        };
    }

    public static string Label(InteractionTargets kind) => kind switch
    {
        InteractionTargets.Npcs => "NPC",
        InteractionTargets.QuestObjects => "Quest object",
        InteractionTargets.Loot => "Loot / treasure",
        InteractionTargets.Aetherytes => "Aetheryte",
        InteractionTargets.GatheringNodes => "Gathering node",
        InteractionTargets.Retainers => "Retainer",
        InteractionTargets.Housing => "Housing",
        InteractionTargets.OtherUsableObjects => "Usable object",
        InteractionTargets.HostileEnemies => "Hostile enemy",
        InteractionTargets.NeutralEnemies => "Neutral enemy",
        _ => "None",
    };
}
