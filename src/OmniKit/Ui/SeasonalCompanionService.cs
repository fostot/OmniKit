using System.Diagnostics;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace OmniKit.Ui;

/// <summary>Read-only framework-thread festival observation; never changes native festival state.</summary>
internal sealed unsafe class SeasonalCompanionService(Configuration config)
{
    private SeasonalCompanionPolicy policy = new();
    private bool catalogLoaded;
    private long nextRead;
    private uint territory;
    private bool? lastEnabled;
    private string? lastSelection;
    private bool? lastReady;
    private SeasonalCompanionSnapshot snapshot = new(null, SeasonalEvidence.LoggedOut, "Seasonal looks resume after login.", []);

    public SeasonalCompanionSnapshot Snapshot => Volatile.Read(ref snapshot);
    public SeasonalCompanionTheme? CurrentTheme => Snapshot.CurrentTheme;
    public string Status => Snapshot.Status;

    public void Update()
    {
        if (!Services.Framework.IsInFrameworkUpdateThread) return;
        if (!Services.ClientState.IsLoggedIn)
        {
            Clear();
            return;
        }
        var game = GameMain.Instance(); var player = PlayerState.Instance();
        var ready = game != null && player != null && game->ConnectedToZone
            && game->TerritoryLoadState == 2 && game->CurrentTerritoryTypeId != 0;
        var currentTerritory = Services.ClientState.TerritoryType;
        var changed = territory != currentTerritory || lastReady != ready
            || lastEnabled != config.CompanionSeasonalEnabled || lastSelection != config.CompanionSeasonalTheme;
        var now = Stopwatch.GetTimestamp();
        if (!changed && now < nextRead) return;
        nextRead = now + Stopwatch.Frequency;
        territory = currentTerritory; lastReady = ready;
        lastEnabled = config.CompanionSeasonalEnabled; lastSelection = config.CompanionSeasonalTheme;
        if (ready && !catalogLoaded)
        {
            // Only static sheet relationships, never the player's quest progress or inventory.
            var relations = Services.Data.GetExcelSheet<Quest>()
                .Where(q => q.Festival.RowId != 0)
                .Select(q => new SeasonalQuestRelation(q.Festival.RowId, q.JournalGenre.RowId));
            policy = new SeasonalCompanionPolicy(relations);
            catalogLoaded = true;
        }
        var world = ready ? new SeasonalFestival[game->ActiveFestivals.Length] : [];
        var ui = ready ? new SeasonalFestival[player->ActiveFestivalIds.Length] : [];
        if (ready)
        {
            for (var i = 0; i < world.Length; i++) world[i] = new(game->ActiveFestivals[i].Id, game->ActiveFestivals[i].Phase);
            for (var i = 0; i < ui.Length; i++) ui[i] = new(player->ActiveFestivalIds[i], player->ActiveFestivalPhases[i]);
        }
        Volatile.Write(ref snapshot, policy.Resolve(config.CompanionSeasonalEnabled, config.CompanionSeasonalTheme,
            true, ready, world, ui, DateTimeOffset.UtcNow));
    }

    public void Clear()
    {
        territory = 0; nextRead = 0; lastReady = null; lastEnabled = null; lastSelection = null;
        if (Snapshot.Evidence != SeasonalEvidence.LoggedOut)
            Volatile.Write(ref snapshot, new(null, SeasonalEvidence.LoggedOut, "Seasonal looks resume after login.", []));
    }
}
