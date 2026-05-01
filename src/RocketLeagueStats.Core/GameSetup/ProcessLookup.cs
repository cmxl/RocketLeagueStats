namespace RocketLeagueStats.Core.GameSetup;

using System.Diagnostics;

public sealed class ProcessLookup : IProcessLookup
{
    public bool IsProcessRunning(string processName)
        => Process.GetProcessesByName(processName).Length > 0;
}
