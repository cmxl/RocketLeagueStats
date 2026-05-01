namespace RocketLeagueStats.Core.Configuration;

public sealed class GameSetupOptions
{
    public const string SectionName = "GameSetup";

    public bool AutoConfigureIni { get; init; } = true;
    public int PacketSendRate { get; init; } = 30;
}
