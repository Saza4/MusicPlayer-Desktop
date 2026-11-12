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
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(FilePath)!, Path.GetFileName(FilePath) + ".corrupt-*"));
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
    }

    [Fact]
    public async Task Load_SettingsFromV130_KeepsEverything()
    {
        // settings.json tal como lo escribe la v1.3.0.
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

        Assert.Equal("negro-puro", loaded.ThemeId);
        Assert.True(loaded.Shuffle);
    }

    // Ajustes retirados de la letra progresiva (spec, M5 y M6): un settings.json que aún los tenga carga bien.
    [Theory]
    [InlineData("\"LyricsStyle\": 1")]   // el interruptor Clásico/Progresivo, retirado en M6
    [InlineData("\"LyricsStyle\": 0")]
    [InlineData("\"ReduceMotion\": true")] // «reducir movimiento», retirado en M5
    [InlineData("\"reduceMotion\": true")]
    [InlineData("\"LyricsStyle\": 1, \"ReduceMotion\": false")]
    public async Task Load_WithRetiredLyricsKeys_LoadsEverythingElse(string retired)
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
              {{retired}}
            }
            """);

        var loaded = await new JsonSettingsRepository(FilePath).LoadAsync();

        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(FilePath)!, "*.corrupt*")); // la clave desconocida no lo daña
        Assert.True(File.Exists(FilePath));
        Assert.Equal(0.4, loaded.Volume);
        Assert.True(loaded.ShowTranslation);
        Assert.Equal(Guid.Parse("7cc26186-a955-49fa-8e61-d524233eeee9"), loaded.LastTrackId);
        Assert.True(loaded.Shuffle);
        Assert.Equal("negro-puro", loaded.ThemeId);
        Assert.False(loaded.ShowIsland);
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