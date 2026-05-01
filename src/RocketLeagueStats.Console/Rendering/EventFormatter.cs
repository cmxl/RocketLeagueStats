namespace RocketLeagueStats.Console.Rendering;

using System.Globalization;
using RocketLeagueStats.Core.Events;
using Spectre.Console;

internal static class EventFormatter
{
    public static string Format(StatsEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);

        var time = (evt.Timestamp ?? DateTimeOffset.UtcNow).ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        return evt switch
        {
            // Payload events
            GoalScoredEvent g => FormatGoal(time, g),
            BallHitEvent b => FormatBallHit(time, b),
            CrossbarHitEvent c => FormatCrossbarHit(time, c),
            StatfeedEvent s => FormatStatfeed(time, s),
            MatchEndedEvent e => $"[grey]{time}[/] [bold green]Match ended[/] — winner: {TeamLabel(e.WinnerTeamNum)}",
            ClockUpdatedSecondsEvent t => FormatClock(time, t),

            // Marker (MatchGuid-only) events — pretty-named, colour-coded by category
            MatchCreatedEvent => Marker(time, "Match created", "green"),
            MatchInitializedEvent => Marker(time, "Match initialized", "green"),
            MatchDestroyedEvent => Marker(time, "Match destroyed", "grey"),
            MatchPausedEvent => Marker(time, "Match paused", "yellow"),
            MatchUnpausedEvent => Marker(time, "Match unpaused", "yellow"),
            CountdownBeginEvent => Marker(time, "Countdown begin", "cyan"),
            RoundStartedEvent => Marker(time, "Round started", "green"),
            GoalReplayStartEvent => Marker(time, "Goal replay start", "magenta"),
            GoalReplayWillEndEvent => Marker(time, "Goal replay will end", "magenta"),
            GoalReplayEndEvent => Marker(time, "Goal replay end", "magenta"),
            ReplayCreatedEvent => Marker(time, "Replay created", "magenta"),
            PodiumStartEvent => Marker(time, "Podium start", "bold green"),
            ReplayPlaybackStartEvent => Marker(time, "Replay playback start", "magenta"),
            ReplayWillEndEvent => Marker(time, "Replay will end", "magenta"),
            ReplayPlaybackEndEvent => Marker(time, "Replay playback end", "magenta"),

            // Forward-compat fallback + periodic state (suppressed)
            UnknownDiscreteEvent u => $"[grey]{time}[/] [yellow]unknown:[/]{Markup.Escape(u.EventName)} — {Markup.Escape(Truncate(u.RawData.GetRawText(), 80))}",
            MatchStateSnapshot => string.Empty,   // suppressed in default mode (30 PPS)
            _ => $"[grey]{time}[/] {Markup.Escape(evt.EventName)}",
        };
    }

    private static string Marker(string time, string label, string color) =>
        $"[grey]{time}[/] [{color}]{label}[/]";

    private static string FormatGoal(string time, GoalScoredEvent g)
    {
        var assist = g.Assister is { } a ? $" (assist: {Markup.Escape(a.Name)})" : string.Empty;
        var speed = g.GoalSpeed.ToString("F0", CultureInfo.InvariantCulture);
        return $"[grey]{time}[/] [bold yellow]GOAL[/] — {Markup.Escape(g.Scorer.Name)}{assist} · {speed} UU/s ({TeamLabel(g.Scorer.TeamNum)})";
    }

    private static string FormatBallHit(string time, BallHitEvent b)
    {
        var players = b.Players.Count == 0
            ? "(unknown)"
            : string.Join(", ", b.Players.Select(p => Markup.Escape(p.Name)));
        var pre = b.Ball.PreHitSpeed.ToString("F0", CultureInfo.InvariantCulture);
        var post = b.Ball.PostHitSpeed.ToString("F0", CultureInfo.InvariantCulture);
        return $"[grey]{time}[/] BallHit — {players} · {pre} → {post} UU/s";
    }

    private static string FormatCrossbarHit(string time, CrossbarHitEvent c)
    {
        var who = c.BallLastTouch is { } t ? $" by {Markup.Escape(t.Player.Name)}" : string.Empty;
        var speed = c.BallSpeed.ToString("F0", CultureInfo.InvariantCulture);
        return $"[grey]{time}[/] [bold]Crossbar hit[/]{who} · {speed} UU/s";
    }

    private static string FormatStatfeed(string time, StatfeedEvent s)
    {
        var label = string.IsNullOrEmpty(s.Type) ? s.StatName : s.Type;
        var main = Markup.Escape(s.MainTarget.Name);
        var details = s.SecondaryTarget is { } secondary
            ? $"{main} → {Markup.Escape(secondary.Name)}"
            : main;
        var color = string.Equals(s.StatName, "Demolish", StringComparison.Ordinal) ? "red" : "cyan";
        return $"[grey]{time}[/] [{color}]{Markup.Escape(label)}[/] — {details}";
    }

    private static string FormatClock(string time, ClockUpdatedSecondsEvent t)
    {
        var ot = t.Overtime ? " [bold red](overtime)[/]" : string.Empty;
        return $"[grey]{time}[/] Clock — {t.TimeSeconds.ToString(CultureInfo.InvariantCulture)}s{ot}";
    }

    private static string TeamLabel(int teamNum) => teamNum switch
    {
        0 => "[blue]Blue[/]",
        1 => "[orange1]Orange[/]",
        _ => $"team {teamNum.ToString(CultureInfo.InvariantCulture)}",
    };

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "...";
}
