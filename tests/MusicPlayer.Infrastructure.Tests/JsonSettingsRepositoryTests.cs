using MusicPlayer.Core.Models;
using MusicPlayer.Infrastructure;

namespace MusicPlayer.Infrastructure.Tests;

public class JsonSettingsRepositoryTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("settings-tests").FullName;
    private string FilePath => Path.Combine(_dir, "nested", "settings.json");

    [Fact]
    public async Task SaveThenLoad_RoundTripsEverything()
    {
        var settings = new AppSettings(0.35, true, true, true, true, Guid.NewGuid());

        await new JsonSettingsRepository(FilePath).SaveAsync(settings);
        var loaded = await new JsonSettingsRepository(FilePath).LoadAsync();

        Assert.Equal(settings, loaded);
    }

    [Fact]
    public async Task Load_MissingFile_ReturnsDefaults()
    {
        var loaded = await new JsonSettingsRepository(FilePath).LoadAsync();

        Assert.Equal(AppSettings.Default, loaded);
    }

    [Fact]
    public async Task Load_CorruptFile_ReturnsDefaultsAndKeepsBackup()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        await File.WriteAllTextAsync(FilePath, "{ esto no es json");

        var loaded = await new JsonSettingsRepository(FilePath).LoadAsync();

        Assert.Equal(AppSettings.Default, loaded);
        Assert.True(File.Exists(FilePath + ".corrupt"));
        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public async Task Load_PartialFile_FillsMissingValuesWithDefaults()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        await File.WriteAllTextAsync(FilePath, "{\"Volume\":0.3}");

        var loaded = await new JsonSettingsRepository(FilePath).LoadAsync();

        Assert.Equal(0.3, loaded.Volume);
        Assert.False(loaded.ShowTranslation);
        Assert.Null(loaded.LastTrackId);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}