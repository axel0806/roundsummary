using CounterStrikeSharp.API;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace RoundSummary;

// After every live round (pug, scrim or match) prints, in green chat text, the score and for each player one line
// per enemy: damage dealt to and taken from them this round, and their HP at the end of the round.
// Damage is counted from player_hurt events and kept per SteamID, so it survives reconnects.
public class RoundSummaryConfig : BasePluginConfig
{
    /// <summary>The tag at the start of every line, e.g. "[MatchZy]". Leave empty for no tag.</summary>
    [JsonPropertyName("Prefix")]
    public string Prefix { get; set; } = "[MatchZy]";
}

public class RoundSummaryPlugin : BasePlugin, IPluginConfig<RoundSummaryConfig>
{
    // Written to addons/counterstrikesharp/configs/plugins/RoundSummary/RoundSummary.json on first load.
    public RoundSummaryConfig Config { get; set; } = new();

    public void OnConfigParsed(RoundSummaryConfig config) => Config = config;

    public override string ModuleName => "RoundSummary";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "local";
    public override string ModuleDescription => "Green end-of-round score and damage report.";

    private string Tag(string text) => Config.Prefix.Length == 0 ? text : $"{Config.Prefix} {text}";
    private const char Green = '\x04';
    private const char Gray = '\x08';

    private record struct Tally(int Damage, int Hits);

    // damage[attackerKey][victimKey]
    private readonly Dictionary<string, Dictionary<string, Tally>> _damage = new();
    private readonly Dictionary<string, string> _names = new();

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
        RegisterEventHandler<EventBeginNewMatch>(OnBeginNewMatch);
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
    }

    private static string Key(CCSPlayerController p) =>
        p.IsBot ? $"bot:{p.PlayerName}" : p.SteamID.ToString();

    private static bool InWarmup() =>
        Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules")
            .FirstOrDefault()?.GameRules?.WarmupPeriod ?? false;

    private void Reset()
    {
        _damage.Clear();
        _names.Clear();
    }

    private HookResult OnBeginNewMatch(EventBeginNewMatch ev, GameEventInfo info)
    {
        Reset();
        return HookResult.Continue;
    }

    // Each round starts with a clean slate.
    private HookResult OnRoundStart(EventRoundStart ev, GameEventInfo info)
    {
        Reset();
        return HookResult.Continue;
    }

    private HookResult OnRoundEnd(EventRoundEnd ev, GameEventInfo info)
    {
        if (InWarmup()) return HookResult.Continue;
        // Wait a moment so the round's score has been added.
        AddTimer(0.5f, Report);
        return HookResult.Continue;
    }

    private HookResult OnPlayerHurt(EventPlayerHurt ev, GameEventInfo info)
    {
        var attacker = ev.Attacker;
        var victim = ev.Userid;
        if (attacker == null || victim == null || !attacker.IsValid || !victim.IsValid) return HookResult.Continue;
        if (attacker.Slot == victim.Slot || attacker.TeamNum == victim.TeamNum) return HookResult.Continue;
        if (attacker.TeamNum < 2 || victim.TeamNum < 2 || InWarmup()) return HookResult.Continue;
        if (ev.DmgHealth <= 0) return HookResult.Continue;

        var a = Key(attacker);
        var v = Key(victim);
        _names[a] = attacker.PlayerName;
        _names[v] = victim.PlayerName;
        if (!_damage.TryGetValue(a, out var row)) _damage[a] = row = new();
        row.TryGetValue(v, out var tally);
        row[v] = new Tally(tally.Damage + ev.DmgHealth, tally.Hits + 1);
        return HookResult.Continue;
    }

    private Tally Dealt(string from, string to) =>
        _damage.TryGetValue(from, out var row) && row.TryGetValue(to, out var t) ? t : default;

    private static string TeamName(int teamNum)
    {
        var team = Utilities.FindAllEntitiesByDesignerName<CCSTeam>("cs_team_manager")
            .FirstOrDefault(t => t.TeamNum == teamNum);
        var name = team?.Teamname;
        return !string.IsNullOrWhiteSpace(name) ? name : teamNum == 3 ? "Counter-Terrorists" : "Terrorists";
    }

    private static int TeamScore(int teamNum) =>
        Utilities.FindAllEntitiesByDesignerName<CCSTeam>("cs_team_manager")
            .FirstOrDefault(t => t.TeamNum == teamNum)?.Score ?? 0;

    private static void Say(CCSPlayerController player, string text) =>
        player.PrintToChat($" {Green}{text}");

    private void Report()
    {
        var players = Utilities.GetPlayers().Where(p => p.IsValid && p.TeamNum >= 2).ToList();
        var score = Tag($"{TeamName(3)} [{TeamScore(3)}-{TeamScore(2)}] {TeamName(2)}");

        foreach (var me in players.Where(p => !p.IsBot))
        {
            Say(me, score);
            foreach (var enemy in players.Where(p => p.TeamNum != me.TeamNum).OrderBy(p => p.Slot))
            {
                var to = Dealt(Key(me), Key(enemy));
                var from = Dealt(Key(enemy), Key(me));
                var pawn = enemy.PlayerPawn.Value;
                var hp = enemy.PawnIsAlive && pawn != null ? Math.Max(pawn.Health, 0) : 0;
                Say(me, Tag($"To: [{to.Damage}/{to.Hits} hits] From: [{from.Damage}/{from.Hits} hits] - {enemy.PlayerName} [{hp} hp]"));
            }
        }

        // Spectators and GOTV get the score only.
        foreach (var spec in Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot && p.TeamNum < 2))
            Say(spec, score);
    }
}
