namespace RocketLeagueStats.WebApi.Services;

using System.Text.Json;
using RocketLeagueStats.WebApi.Contracts;

internal sealed class SettingsStore(string directoryPath) : ISettingsStore
{
    private static readonly SettingsDto Defaults = new(
        PlayerName: null,
        FriendNames: [],
        ShowTrainingInHistory: false);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string filePath = Path.Combine(directoryPath, "settings.json");

    public async Task<SettingsDto> GetAsync(CancellationToken ct)
    {
        if (!File.Exists(this.filePath))
        {
            return Defaults;
        }

        try
        {
            await using var fs = File.OpenRead(this.filePath);
            var dto = await JsonSerializer.DeserializeAsync<SettingsDto>(fs, JsonOptions, ct);
            return dto ?? Defaults;
        }
        catch (JsonException)
        {
            this.BackupCorruptedFile();
            return Defaults;
        }
    }

    public async Task SaveAsync(SettingsDto settings, CancellationToken ct)
    {
        Directory.CreateDirectory(directoryPath);
        await using var fs = File.Create(this.filePath);
        await JsonSerializer.SerializeAsync(fs, settings, JsonOptions, ct);
    }

    private void BackupCorruptedFile()
    {
        var backupName = $"settings.json.bad-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var backupPath = Path.Combine(directoryPath, backupName);
        File.Move(this.filePath, backupPath);
    }
}
