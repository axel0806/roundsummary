# RoundSummary

A [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) plugin for CS2. After every live round it prints, in green chat text, the score and one line per enemy for each player:

```
[MatchZy] TeamA [3-2] TeamB
[MatchZy] To: [87/2 hits] From: [34/1 hits] - PLAYERNAME [0 hp]
```

- `To` is the damage and number of hits you dealt to that enemy this round, `From` is what they dealt to you, and the last part is their HP when the round ended (0 if dead).
- Damage resets every round. Warmup rounds print nothing. Team damage is ignored.
- Spectators only see the score line.

## Config

CounterStrikeSharp writes `addons/counterstrikesharp/configs/plugins/RoundSummary/RoundSummary.json` on first load:

```json
{
  "Prefix": "[MatchZy]"
}
```

`Prefix` is the tag at the start of each line. Set it to `""` for no tag. Run `css_plugins reload RoundSummary` after editing.

## Build and install

Needs the .NET 10 SDK (CounterStrikeSharp v1.0.379 targets .NET 10).

```sh
dotnet build -c Release -o dist
```

Copy `dist/RoundSummary.dll` and `dist/RoundSummary.deps.json` to `addons/counterstrikesharp/plugins/RoundSummary/` on the server, then run `css_plugins load RoundSummary`.
