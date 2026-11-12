using System.Text;
using MusicPlayer.Core.Models;
using MusicPlayer.Infrastructure;

namespace MusicPlayer.Infrastructure.Tests;

public class JsonThemeRepositoryTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("theme-tests").FullName;

    private const string Valid = """
        {
          "formatVersion": 1,
          "name": "Medianoche",
          "base": "dark",
          "colors": { "background": "#0B0F1A", "primary": "#7AA2F7", "accent": "#7AA2F7" }
        }
        """;

    private string Write(string fileName, string content)
    {
        var path = Path.Combine(_dir, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    private Task<ThemeLoadReport> Load() => new JsonThemeRepository(_dir).LoadAllAsync();

    [Fact]
    public async Task Load_MissingFolder_ReturnsEmpty()
    {
        var report = await new JsonThemeRepository(Path.Combine(_dir, "no-existe")).LoadAllAsync();

        Assert.Empty(report.Themes);
        Assert.Empty(report.Errors);
    }

    [Fact]
    public async Task Load_ValidFile_ReturnsTheTheme_WithTheFileNameAsId()
    {
        Write("medianoche.json", Valid);

        var report = await Load();

        var theme = Assert.Single(report.Themes);
        Assert.Equal("medianoche", theme.Id);
        Assert.Equal("Medianoche", theme.Name);
        Assert.Equal("#0B0F1A", theme.Palette.Background.ToHex());
        Assert.Empty(report.Errors);
    }

    [Fact]
    public async Task Load_CorruptJson_IsAnErrorAndDoesNotThrow()
    {
        Write("roto.json", "{ esto no es json");

        var report = await Load();

        Assert.Empty(report.Themes);
        var error = Assert.Single(report.Errors);
        Assert.Equal("roto.json", error.File);
    }

    [Fact]
    public async Task Load_BadFileDoesNotAffectTheGoodOnes()
    {
        Write("a-bueno.json", Valid);
        Write("b-roto.json", "[]");
        Write("c-bueno.json", Valid);

        var report = await Load();

        Assert.Equal(["a-bueno", "c-bueno"], report.Themes.Select(t => t.Id));
        Assert.Single(report.Errors);
    }

    [Fact]
    public async Task Load_Utf8Bom_IsAccepted()
    {
        File.WriteAllBytes(Path.Combine(_dir, "bom.json"),
            [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(Valid)]);

        var report = await Load();

        Assert.Single(report.Themes);
    }

    [Fact]
    public async Task Load_CommentsAndTrailingCommas_AreTolerated()
    {
        Write("manual.json", """
            {
              // tema escrito a mano
              "formatVersion": 1,
              "colors": { "background": "#101820", },
            }
            """);

        var report = await Load();

        Assert.Single(report.Themes);
        Assert.Empty(report.Errors);
    }

    [Fact]
    public async Task Load_EmptyFile_IsAnError()
    {
        Write("vacio.json", "");

        var report = await Load();

        Assert.Single(report.Errors);
    }

    [Fact]
    public async Task Load_FileOverTheSizeLimit_IsAnError()
    {
        Write("grande.json", Valid + new string(' ', (int)JsonThemeRepository.MaxFileBytes));

        var report = await Load();

        Assert.Empty(report.Themes);
        Assert.Contains(report.Errors, e => e.File == "grande.json" && e.Message.Contains("tamaño"));
    }

    [Fact]
    public async Task Load_MoreThanTheMaximumFiles_ReadsOnlyTheFirstOnesAndWarns()
    {
        for (var i = 0; i < JsonThemeRepository.MaxFiles + 5; i++)
            Write($"t{i:000}.json", Valid);

        var report = await Load();

        Assert.Equal(JsonThemeRepository.MaxFiles, report.Themes.Count);
        Assert.Single(report.Warnings);
    }

    [Fact]
    public async Task Load_NonJsonFiles_AreIgnored()
    {
        Write("notas.txt", "hola");
        Write("tema.json.bak", Valid);

        var report = await Load();

        Assert.Empty(report.Themes);
        Assert.Empty(report.Errors);
    }

    [Theory]
    [InlineData("dark.json")]
    [InlineData("Light.json")]
    [InlineData("system.json")]
    public async Task Load_ReservedFileName_IsAnError(string fileName)
    {
        Write(fileName, Valid);

        var report = await Load();

        Assert.Empty(report.Themes);
        Assert.Single(report.Errors);
    }

    [Fact]
    public async Task Load_AccentedFileAndName_Work()
    {
        Write("canción.json", Valid.Replace("Medianoche", "Música de noche"));

        var report = await Load();

        var theme = Assert.Single(report.Themes);
        Assert.Equal("canción", theme.Id);
        Assert.Equal("Música de noche", theme.Name);
    }

    [Fact]
    public async Task Load_WrongTypes_AreErrorsNotExceptions()
    {
        Write("version-texto.json", """{ "formatVersion": "1" }""");
        Write("base-numero.json", """{ "formatVersion": 1, "base": 3 }""");

        var report = await Load();

        Assert.Empty(report.Themes);
        Assert.Equal(2, report.Errors.Count);
    }

    [Fact]
    public async Task Load_InvalidColor_IsAWarning_AndTheThemeStillLoads()
    {
        Write("aviso.json", """{ "formatVersion": 1, "colors": { "danger": "rojo", "extra": "#123456" } }""");

        var report = await Load();

        Assert.Single(report.Themes);
        Assert.Equal(2, report.Warnings.Count);
        Assert.All(report.Warnings, w => Assert.Equal("aviso.json", w.File));
    }

    [Fact]
    public async Task Load_ThemeWithoutContrast_IsRejectedWithTheReason()
    {
        Write("ilegible.json", """{ "formatVersion": 1, "base": "dark", "colors": { "background": "#FFFFFF" } }""");

        var report = await Load();

        Assert.Empty(report.Themes);
        Assert.Contains(report.Errors, e => e.Message.Contains("contraste"));
    }

    // Un sustituto suelto escapado pasa el análisis del JSON y lanza al leer el texto (InvalidOperationException):
    // es un error de ese archivo, no del repositorio (docs/specs/audit-followup.md, criterio 4).
    [Theory]
    [InlineData("""{ "formatVersion": 1, "name": "\uD800" }""")]
    [InlineData("""{ "formatVersion": 1, "name": "x", "colors": { "background": "\uDC00" } }""")]
    [InlineData("""{ "formatVersion": 1, "name": "x", "\uD800": 1 }""")]
    public async Task Load_LoneSurrogateEscape_IsAnErrorOfThatFile_AndTheRestLoad(string broken)
    {
        Write("a-roto.json", broken);
        Write("medianoche.json", Valid);

        var report = await Load();

        Assert.Equal("medianoche", Assert.Single(report.Themes).Id);
        Assert.Equal("a-roto.json", Assert.Single(report.Errors).File);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }
}
