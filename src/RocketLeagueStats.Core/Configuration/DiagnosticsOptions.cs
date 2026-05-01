namespace RocketLeagueStats.Core.Configuration;

/// <summary>
/// Diagnostic toggles for capturing extra detail without bloating the normal JSONL stream.
/// All options default to off — production runs should leave them disabled.
/// </summary>
public sealed class DiagnosticsOptions
{
    public const string SectionName = "Diagnostics";

    /// <summary>
    /// When true, the first <c>MatchStateSnapshot</c> received during each match is written
    /// as raw JSON to a file under <see cref="Directory"/>. Used to capture the wire-format
    /// shape so the projector's snapshot parser can be written/updated.
    /// </summary>
    public bool DumpSnapshots { get; init; }

    /// <summary>Directory to write snapshot dumps to. Defaults to <c>logs/snapshots</c>.</summary>
    public string? Directory { get; init; }
}
