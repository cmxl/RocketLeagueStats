namespace RocketLeagueStats.Core.GameSetup;

using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

public sealed class StatsApiConfigWriter(IProcessLookup processLookup, ILogger<StatsApiConfigWriter> logger) : IStatsApiConfigWriter
{
    private const string SectionName = "TAGame.MatchStatsExporter_TA";
    private const string ProcessName = "RocketLeague";

    private static readonly Action<ILogger, string, Exception?> LogGameRunningSkip =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(1, nameof(StatsApiConfigWriter)), "Rocket League is running — skipping ini write at {Path}");

    private static readonly Action<ILogger, string, Exception?> LogAlreadyConfigured =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(2, nameof(StatsApiConfigWriter)), "Stats API config already correct at {Path}");

    private static readonly Action<ILogger, string, Exception?> LogBackupCreated =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(3, nameof(StatsApiConfigWriter)), "Backed up original ini to {BackupPath}");

    private static readonly Action<ILogger, string, string, string, Exception?> LogKeySet =
        LoggerMessage.Define<string, string, string>(LogLevel.Information, new EventId(4, nameof(StatsApiConfigWriter)), "Set [{Section}] {Key} in {Path}");

    public StatsApiConfigOutcome EnsureConfigured(RocketLeagueInstall install, StatsApiConfigDesired desired)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(desired);

        var iniPath = Path.Combine(install.Path, "TAGame", "Config", "DefaultStatsAPI.ini");

        if (processLookup.IsProcessRunning(ProcessName))
        {
            LogGameRunningSkip(logger, iniPath, null);
            return new StatsApiConfigOutcome(false, [], "Rocket League is running; close the game and retry.");
        }

        var existed = File.Exists(iniPath);
        var current = existed
            ? IniDocument.Parse(File.ReadAllText(iniPath))
            : IniDocument.Empty();

        var changed = new List<string>();
        if (current.GetIntValue(SectionName, "PacketSendRate") != desired.PacketSendRate)
        {
            current.SetValue(SectionName, "PacketSendRate", desired.PacketSendRate.ToString(CultureInfo.InvariantCulture));
            changed.Add("PacketSendRate");
        }

        if (current.GetIntValue(SectionName, "Port") != desired.Port)
        {
            current.SetValue(SectionName, "Port", desired.Port.ToString(CultureInfo.InvariantCulture));
            changed.Add("Port");
        }

        if (changed.Count == 0)
        {
            LogAlreadyConfigured(logger, iniPath, null);
            return new StatsApiConfigOutcome(false, [], "ini already configured");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(iniPath)!);

        if (existed)
        {
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            var backupPath = $"{iniPath}.bak.{stamp}";
            if (!File.Exists(backupPath))
            {
                File.Copy(iniPath, backupPath);
                LogBackupCreated(logger, backupPath, null);
            }
        }

        File.WriteAllText(iniPath, current.Render());

        foreach (var key in changed)
        {
            LogKeySet(logger, SectionName, key, iniPath, null);
        }

        return new StatsApiConfigOutcome(true, changed, "ini updated");
    }

    /// <summary>
    /// Minimal INI document model that preserves comment/blank-line whitespace and per-line terminators
    /// (CRLF / LF / CR), so a round-trip through <see cref="Render"/> is byte-identical when no values change.
    /// </summary>
    private sealed class IniDocument
    {
        private readonly List<IniLine> lines = [];
        private string defaultTerminator = Environment.NewLine;

        public static IniDocument Parse(string content)
        {
            var doc = new IniDocument();
            var firstTerminatorRecorded = false;
            var i = 0;
            while (i < content.Length)
            {
                var lineStart = i;
                while (i < content.Length && content[i] != '\r' && content[i] != '\n')
                {
                    i++;
                }

                var lineContent = content[lineStart..i];
                var terminator = string.Empty;
                if (i < content.Length)
                {
                    if (content[i] == '\r')
                    {
                        if (i + 1 < content.Length && content[i + 1] == '\n')
                        {
                            terminator = "\r\n";
                            i += 2;
                        }
                        else
                        {
                            terminator = "\r";
                            i += 1;
                        }
                    }
                    else
                    {
                        terminator = "\n";
                        i += 1;
                    }

                    if (!firstTerminatorRecorded)
                    {
                        doc.defaultTerminator = terminator;
                        firstTerminatorRecorded = true;
                    }
                }

                doc.lines.Add(IniLine.From(lineContent, terminator));
            }

            return doc;
        }

        public static IniDocument Empty() => new();

        public int? GetIntValue(string section, string key)
        {
            var line = this.FindKvLine(section, key);
            if (line is null)
            {
                return null;
            }

            return int.TryParse(line.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
                ? v
                : null;
        }

        public void SetValue(string section, string key, string value)
        {
            var existing = this.FindKvLine(section, key);
            if (existing is not null)
            {
                existing.Value = value;
                return;
            }

            // Insert at the end of the section, or create the section if missing.
            var sectionStart = this.lines.FindIndex(l =>
                l.IsSectionHeader && string.Equals(l.SectionName, section, StringComparison.OrdinalIgnoreCase));
            if (sectionStart < 0)
            {
                if (this.lines.Count > 0)
                {
                    this.EnsureTerminator(this.lines.Count - 1);
                    if (!string.IsNullOrWhiteSpace(this.lines[^1].RawText))
                    {
                        this.lines.Add(IniLine.Blank(this.defaultTerminator));
                    }
                }

                this.lines.Add(IniLine.SectionHeader(section, this.defaultTerminator));
                this.lines.Add(IniLine.KeyValue(section, key, value, this.defaultTerminator));
                return;
            }

            // Find first index >= sectionStart+1 that is another section header (or end).
            var insertAt = this.lines.FindIndex(sectionStart + 1, l => l.IsSectionHeader);
            if (insertAt < 0)
            {
                insertAt = this.lines.Count;
            }

            if (insertAt > 0)
            {
                this.EnsureTerminator(insertAt - 1);
            }

            this.lines.Insert(insertAt, IniLine.KeyValue(section, key, value, this.defaultTerminator));
        }

        public string Render()
        {
            var sb = new StringBuilder();
            foreach (var line in this.lines)
            {
                sb.Append(line.Render());
                sb.Append(line.Terminator);
            }

            return sb.ToString();
        }

        private void EnsureTerminator(int index)
        {
            if (string.IsNullOrEmpty(this.lines[index].Terminator))
            {
                this.lines[index].Terminator = this.defaultTerminator;
            }
        }

        private IniLine? FindKvLine(string section, string key)
        {
            string? currentSection = null;
            foreach (var line in this.lines)
            {
                if (line.IsSectionHeader)
                {
                    currentSection = line.SectionName;
                    continue;
                }

                if (line.IsKeyValue
                    && string.Equals(currentSection, section, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(line.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return line;
                }
            }

            return null;
        }

        private sealed class IniLine
        {
            public string RawText { get; private set; } = string.Empty;

            public bool IsSectionHeader { get; private set; }

            public bool IsKeyValue { get; private set; }

            public string? SectionName { get; private set; }

            public string? Key { get; private set; }

            public string? Value { get; set; }

            public string Terminator { get; set; } = string.Empty;

            public static IniLine From(string raw, string terminator)
            {
                var trimmed = raw.Trim();
                var line = new IniLine { RawText = raw, Terminator = terminator };
                if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
                {
                    line.IsSectionHeader = true;
                    line.SectionName = trimmed[1..^1].Trim();
                    return line;
                }

                if (trimmed.Length == 0
                    || trimmed.StartsWith(';')
                    || trimmed.StartsWith('#'))
                {
                    return line; // comment or blank — preserved as raw
                }

                var eq = raw.IndexOf('=', StringComparison.Ordinal);
                if (eq <= 0)
                {
                    return line;
                }

                line.IsKeyValue = true;
                line.Key = raw[..eq].Trim();
                line.Value = raw[(eq + 1)..].Trim();
                return line;
            }

            public static IniLine SectionHeader(string section, string terminator) =>
                new() { IsSectionHeader = true, SectionName = section, RawText = $"[{section}]", Terminator = terminator };

            public static IniLine KeyValue(string section, string key, string value, string terminator) =>
                new() { IsKeyValue = true, SectionName = section, Key = key, Value = value, RawText = string.Empty, Terminator = terminator };

            public static IniLine Blank(string terminator) => new() { RawText = string.Empty, Terminator = terminator };

            public string Render()
            {
                if (this.IsSectionHeader)
                {
                    return $"[{this.SectionName}]";
                }

                if (this.IsKeyValue)
                {
                    return $"{this.Key}={this.Value}";
                }

                return this.RawText;
            }
        }
    }
}
