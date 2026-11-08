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
        var settings = new AppSettings(0.35, true, true, true, true, Guid.NewGuid(), Shuffle: true, Repeat: RepeatMode.One);

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
        Assert.Equal("dark", loaded.ThemeId); // un settings.json anterior a los temas sigue en Oscuro
        Assert.True(loaded.ShowIsland);       // y uno anterior a la isla la recibe activada
        Assert.Equal(LyricsStyle.Classic, loaded.LyricsStyle);
    }

    [Fact]
    public async Task Load_SettingsFromV130_KeepClassicLyrics()
    {
        // settings.json tal como lo escribe la v1.3.0: completo, pero anterior a LyricsStyle.
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        await File.WriteAllTextAsync(FilePath, """
            {
              "Volume": 1,
              "ShowTranslation": false,
              "ShowPronunciation": false,
              "HideOriginal": false,
              "EmphasizePronunciation": false,
              "LastTrackId": "7cc26186-a955-49fa-8e61-d524233eeee9",
              "Shuffle": true,
              "Repeat": 0,
              "ThemeId": "negro-puro",
              "ShowIsland": true
            }
            """);

        var loaded = await new JsonSettingsRepository(FilePath).LoadAsync();

        Assert.Equal(LyricsStyle.Classic, loaded.LyricsStyle);
        Assert.Equal("negro-puro", loaded.ThemeId); // y lo demás se conserva
        Assert.True(loaded.Shuffle);
    }

    [Theory]
    [InlineData("ReduceMotion")] // como lo escribía la app en las pruebas de M5 (ajuste ya retirado)
    [InlineData("reduceMotion")]
    public async Task Load_WithRetiredReduceMotionKey_LoadsEverythingElse(string key)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        await File.WriteAllTextAsync(FilePath, $$"""
            {
              "Volume": 0.4,
              "ShowTranslation": true,
              "LastTrackId": "7cc26186-a955-49fa-8e61-d524233eeee9",
              "Shuffle": true,
              "ThemeId": "negro-puro",
              "ShowIsland": false,
              "LyricsStyle": 1,
              "{{key}}": true
            }
            """);

        var loaded = await new JsonSettingsRepository(FilePath).LoadAsync();

        Assert.False(File.Exists(FilePath + ".corrupt")); // la clave desconocida no lo daña
        Assert.True(File.Exists(FilePath));
        Assert.Equal(0.4, loaded.Volume);
        Assert.True(loaded.ShowTranslation);
        Assert.Equal(Guid.Parse("7cc26186-a955-49fa-8e61-d524233eeee9"), loaded.LastTrackId);
        Assert.True(loaded.Shuffle);
        Assert.Equal("negro-puro", loaded.ThemeId);
        Assert.False(loaded.ShowIsland);
        Assert.Equal(LyricsStyle.Progressive, loaded.LyricsStyle);
    }

    [Fact]
    public async Task SaveThenLoad_KeepsTheLyricsStyle()
    {
        await new JsonSettingsRepository(FilePath).SaveAsync(AppSettings.Default with { LyricsStyle = LyricsStyle.Progressive });

        var loaded = await new JsonSettingsRepository(FilePath).LoadAsync();

        Assert.Equal(LyricsStyle.Progressive, loaded.LyricsStyle);
    }

    [Fact]
    public async Task SaveThenLoad_KeepsTheIslandTurnedOff()
    {
        await new JsonSettingsRepository(FilePath).SaveAsync(AppSettings.Default with { ShowIsland = false });

        var loaded = await new JsonSettingsRepository(FilePath).LoadAsync();

        Assert.False(loaded.ShowIsland);
    }

    [Fact]
    public async Task SaveThenLoad_KeepsTheTheme()
    {
        await new JsonSettingsRepository(FilePath).SaveAsync(AppSettings.Default with { ThemeId = "medianoche" });

        var loaded = await new JsonSettingsRepository(FilePath).LoadAsync();

        Assert.Equal("medianoche", loaded.ThemeId);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}