namespace OmniKit.Interaction;

[Flags]
public enum InteractionTargets
{
    None = 0,
    Npcs = 1,
    QuestObjects = 2,
    Loot = 4,
    Aetherytes = 8,
    GatheringNodes = 16,
    Retainers = 32,
    Housing = 64,
    OtherUsableObjects = 128,
    HostileEnemies = 256,
    NeutralEnemies = 512,
    DefaultWorld = Npcs | QuestObjects | Loot | Aetherytes | GatheringNodes | Retainers | Housing | OtherUsableObjects,
    All = DefaultWorld | HostileEnemies | NeutralEnemies,
}

public enum EnemyInteractMode
{
    Choose = 0,
    TargetOnly = 1,
    StartAttack = 2,
}
