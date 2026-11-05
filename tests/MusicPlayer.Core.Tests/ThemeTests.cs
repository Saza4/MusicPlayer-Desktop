using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

public class ThemeColorTests
{
    [Theory]
    [InlineData("#0F0F0F", 15, 15, 15)]
    [InlineData("#ff8000", 255, 128, 0)]
    [InlineData("  #1A73E8 ", 26, 115, 232)]
    public void TryParse_ValidHex_ReturnsColor(string text, byte r, byte g, byte b)
    {
        Assert.True(ThemeColor.TryParse(text, out var color));
        Assert.Equal(new ThemeColor(r, g, b), color);
    }

    [Theory]
    [InlineData("#FFF")]
    [InlineData("#80FFFFFF")]
    [InlineData("rojo")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("FFFFFF")]
    [InlineData("#GGGGGG")]
    [InlineData("#+12345")]
    public void TryParse_InvalidText_ReturnsFalse(string? text)
    {
        Assert.False(ThemeColor.TryParse(text, out _));
    }

    [Fact]
    public void ToHex_RoundTrips()
    {
        Assert.Equal("#1A73E8", ThemeColor.Parse("#1a73e8").ToHex());
    }
}

public class ColorContrastTests
{
    [Fact]
    public void Ratio_BlackAndWhite_Is21()
    {
        Assert.Equal(21.0, ColorContrast.Ratio(new ThemeColor(0, 0, 0), new ThemeColor(255, 255, 255)), 3);
    }

    [Fact]
    public void Ratio_SameColor_Is1_AndIsSymmetric()
    {
        var a = ThemeColor.Parse("#336699");
        var b = ThemeColor.Parse("#FFCC00");
        Assert.Equal(1.0, ColorContrast.Ratio(a, a), 6);
        Assert.Equal(ColorContrast.Ratio(a, b), ColorContrast.Ratio(b, a), 9);
    }
}

public class BuiltInThemesTests
{
    [Fact]
    public void Dark_KeepsTheColorsTheInterfaceAlwaysHad()
    {
        var p = BuiltInThemes.Dark.Palette;
        Assert.Equal("#0F0F0F", p.Background.ToHex());
        Assert.Equal("#000000", p.Surface.ToHex());
        Assert.Equal("#FFFFFF", p.Overlay.ToHex());
        Assert.Equal("#FFFFFF", p.Primary.ToHex());
        Assert.Equal("#101010", p.OnPrimary.ToHex());
        Assert.Null(p.Accent);
        Assert.Equal("#E57373", p.Danger.ToHex());
        Assert.Equal("#4CAF50", p.Success.ToHex());
        Assert.Equal("#FFB74D", p.Warning.ToHex());
    }

    [Theory]
    [InlineData("dark")]
    [InlineData("LIGHT")]
    [InlineData("system")]
    public void IsReservedId_IntegratedIds_AreReserved(string id)
    {
        Assert.True(BuiltInThemes.IsReservedId(id));
    }

    [Fact]
    public void IsReservedId_OtherId_IsNotReserved()
    {
        Assert.False(BuiltInThemes.IsReservedId("medianoche"));
    }

    [Fact]
    public void BuiltInThemes_PassTheirOwnValidationRules()
    {
        foreach (var theme in BuiltInThemes.All)
        {
            var colors = new Dictionary<string, string?>
            {
                ["background"] = theme.Palette.Background.ToHex(),
                ["surface"] = theme.Palette.Surface.ToHex(),
                ["overlay"] = theme.Palette.Overlay.ToHex(),
                ["primary"] = theme.Palette.Primary.ToHex(),
                ["onPrimary"] = theme.Palette.OnPrimary.ToHex(),
                ["danger"] = theme.Palette.Danger.ToHex(),
                ["success"] = theme.Palette.Success.ToHex(),
                ["warning"] = theme.Palette.Warning.ToHex(),
            };

            var result = ThemeFactory.Create("copia-" + theme.Id, 1, theme.Name, theme.Base.ToString(), colors);

            Assert.True(result.Theme is not null, $"{theme.Id}: {string.Join("; ", result.Errors)}");
            Assert.Empty(result.Warnings);
        }
    }
}

public class ThemeFactoryTests
{
    private static Dictionary<string, string?> Full() => new()
    {
        ["background"] = "#0B0F1A",
        ["surface"] = "#05070D",
        ["overlay"] = "#FFFFFF",
        ["primary"] = "#7AA2F7",
        ["onPrimary"] = "#0B0F1A",
        ["accent"] = "#7AA2F7",
        ["danger"] = "#F7768E",
        ["success"] = "#9ECE6A",
        ["warning"] = "#E0AF68",
    };

    [Fact]
    public void Create_CompleteTheme_IsValid()
    {
        var result = ThemeFactory.Create("medianoche", 1, "Medianoche", "dark", Full());

        Assert.NotNull(result.Theme);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
        Assert.Equal("Medianoche", result.Theme!.Name);
        Assert.Equal("#0B0F1A", result.Theme.Palette.Background.ToHex());
        Assert.Equal("#7AA2F7", result.Theme.Palette.Accent!.Value.ToHex());
        Assert.False(result.Theme.IsBuiltIn);
    }

    [Fact]
    public void Create_MissingColors_InheritFromTheBase()
    {
        var dark = ThemeFactory.Create("a", 1, "A", "dark", new Dictionary<string, string?> { ["background"] = "#101820" });
        var light = ThemeFactory.Create("b", 1, "B", "light", new Dictionary<string, string?> { ["background"] = "#EEEEEE" });

        Assert.Equal(BuiltInThemes.Dark.Palette.Surface, dark.Theme!.Palette.Surface);
        Assert.Equal(BuiltInThemes.Light.Palette.Surface, light.Theme!.Palette.Surface);
        Assert.Null(dark.Theme.Palette.Accent);
        Assert.Empty(dark.Warnings);
    }

    [Fact]
    public void Create_NullColors_IsAThemeOfTheBase()
    {
        var result = ThemeFactory.Create("solo-base", 1, null, "light", null);

        Assert.Equal(BuiltInThemes.Light.Palette, result.Theme!.Palette);
        Assert.Equal(ThemeBase.Light, result.Theme.Base);
    }

    [Fact]
    public void Create_InvalidColor_WarnsAndInherits()
    {
        var colors = new Dictionary<string, string?> { ["danger"] = "rojo", ["success"] = "#80FFFFFF", ["warning"] = null };

        var result = ThemeFactory.Create("t", 1, "T", "dark", colors);

        Assert.NotNull(result.Theme);
        Assert.Equal(3, result.Warnings.Count);
        Assert.Equal(BuiltInThemes.Dark.Palette.Danger, result.Theme!.Palette.Danger);
        Assert.Equal(BuiltInThemes.Dark.Palette.Success, result.Theme.Palette.Success);
        Assert.Equal(BuiltInThemes.Dark.Palette.Warning, result.Theme.Palette.Warning);
    }

    [Fact]
    public void Create_InvalidAccent_WarnsAndFallsBackToSystemAccent()
    {
        var result = ThemeFactory.Create("t", 1, "T", "dark", new Dictionary<string, string?> { ["accent"] = "azul" });

        Assert.NotNull(result.Theme);
        Assert.Single(result.Warnings);
        Assert.Null(result.Theme!.Palette.Accent);
    }

    [Fact]
    public void Create_UnknownColorKey_WarnsAndIsIgnored()
    {
        var colors = new Dictionary<string, string?> { ["fondo"] = "#123456" };

        var result = ThemeFactory.Create("t", 1, "T", "dark", colors);

        Assert.NotNull(result.Theme);
        Assert.Contains(result.Warnings, w => w.Contains("fondo"));
    }

    [Fact]
    public void Create_ColorKeysIgnoreCase()
    {
        var colors = new Dictionary<string, string?> { ["BACKGROUND"] = "#202020", ["OnPrimary"] = "#000000" };

        var result = ThemeFactory.Create("t", 1, "T", "dark", colors);

        Assert.Empty(result.Warnings);
        Assert.Equal("#202020", result.Theme!.Palette.Background.ToHex());
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(2)]
    public void Create_UnsupportedFormatVersion_IsAnError(int? version)
    {
        var result = ThemeFactory.Create("t", version, "T", "dark", Full());

        Assert.Null(result.Theme);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Create_NewerFormatVersion_SaysSo()
    {
        var result = ThemeFactory.Create("t", 2, "T", "dark", Full());

        Assert.Contains(result.Errors, e => e.Contains("más nueva"));
    }

    [Fact]
    public void Create_InvalidBase_IsAnError_AndMissingBaseIsDark()
    {
        Assert.Null(ThemeFactory.Create("t", 1, "T", "oscuro", Full()).Theme);
        Assert.Equal(ThemeBase.Dark, ThemeFactory.Create("t", 1, "T", null, Full()).Theme!.Base);
    }

    [Theory]
    [InlineData("dark")]
    [InlineData("Light")]
    [InlineData("system")]
    public void Create_ReservedId_IsAnError(string id)
    {
        var result = ThemeFactory.Create(id, 1, "T", "dark", Full());

        Assert.Null(result.Theme);
        Assert.Contains(result.Errors, e => e.Contains("reservado"));
    }

    [Fact]
    public void Create_EmptyName_UsesTheId_AndLongNameIsTrimmed()
    {
        Assert.Equal("mi-tema", ThemeFactory.Create("mi-tema", 1, "  ", "dark", Full()).Theme!.Name);
        Assert.Equal(ThemeFactory.MaxNameLength,
            ThemeFactory.Create("t", 1, new string('x', 100), "dark", Full()).Theme!.Name.Length);
    }

    [Fact]
    public void Create_TextOnBackgroundWithoutContrast_IsAnError()
    {
        // Base oscura (texto blanco) con fondo claro.
        var colors = Full();
        colors["background"] = "#F0F0F0";

        var result = ThemeFactory.Create("t", 1, "T", "dark", colors);

        Assert.Null(result.Theme);
        Assert.Contains(result.Errors, e => e.Contains("'background'"));
    }

    [Fact]
    public void Create_TextOnSurfaceWithoutContrast_IsAnError()
    {
        var colors = Full();
        colors["surface"] = "#EEEEEE";

        var result = ThemeFactory.Create("t", 1, "T", "dark", colors);

        Assert.Contains(result.Errors, e => e.Contains("'surface'"));
    }

    [Fact]
    public void Create_OnPrimaryWithoutContrast_IsAnError()
    {
        var colors = Full();
        colors["onPrimary"] = colors["primary"];

        var result = ThemeFactory.Create("t", 1, "T", "dark", colors);

        Assert.Contains(result.Errors, e => e.Contains("'onPrimary'"));
    }

    [Fact]
    public void Create_PrimaryTooDarkForTheImmersiveVeil_IsAnError()
    {
        var colors = Full();
        colors["primary"] = "#202020";
        colors["onPrimary"] = "#FFFFFF";

        var result = ThemeFactory.Create("t", 1, "T", "dark", colors);

        Assert.Null(result.Theme);
        Assert.Contains(result.Errors, e => e.Contains("inmersivo"));
    }

    [Fact]
    public void Create_AccentTooDarkForTheImmersiveVeil_IsAnError()
    {
        var colors = Full();
        colors["accent"] = "#101010";

        var result = ThemeFactory.Create("t", 1, "T", "dark", colors);

        Assert.Contains(result.Errors, e => e.Contains("'accent'"));
    }

    [Fact]
    public void Create_MissingAccent_IsNotAnError()
    {
        var colors = Full();
        colors.Remove("accent");

        Assert.NotNull(ThemeFactory.Create("t", 1, "T", "dark", colors).Theme);
    }

    [Fact]
    public void Create_ExactlyAtMinimumContrast_Passes()
    {
        // #767676 sobre blanco es el clásico 4.54:1; #6F6F6F contra negro da justo por encima de 3:1.
        // Se comprueba el límite con un gris cuyo contraste contra negro es >= 3 y otro justo por debajo.
        var ok = Full();
        ok["primary"] = "#6F6F6F"; // ~3.8:1 contra negro
        ok["onPrimary"] = "#000000";
        Assert.NotNull(ThemeFactory.Create("t", 1, "T", "dark", ok).Theme);

        var low = Full();
        low["primary"] = "#4A4A4A"; // ~2.2:1 contra negro
        low["onPrimary"] = "#FFFFFF";
        Assert.Null(ThemeFactory.Create("t", 1, "T", "dark", low).Theme);
    }
}

public class ThemeCatalogTests
{
    private static AppTheme User(string id) =>
        new(id, id, ThemeBase.Dark, BuiltInThemes.Dark.Palette);

    [Fact]
    public void Merge_PutsIntegratedFirst_ThenUserThemes()
    {
        var merged = ThemeCatalog.Merge(new ThemeLoadReport([User("zeta"), User("alfa")], [], []));

        Assert.Equal(["dark", "light", "zeta", "alfa"], merged.Themes.Select(t => t.Id));
    }

    [Fact]
    public void Merge_DuplicateId_KeepsTheFirstAndReportsTheOther()
    {
        var merged = ThemeCatalog.Merge(new ThemeLoadReport([User("a"), User("A")], [], []));

        Assert.Equal(3, merged.Themes.Count);
        Assert.Single(merged.Errors);
    }

    [Fact]
    public void Merge_KeepsTheRepositoryErrorsAndWarnings()
    {
        var error = new ThemeIssue("x.json", "roto");
        var warning = new ThemeIssue("y.json", "aviso");

        var merged = ThemeCatalog.Merge(new ThemeLoadReport([], [error], [warning]));

        Assert.Contains(error, merged.Errors);
        Assert.Contains(warning, merged.Warnings);
    }

    [Fact]
    public void Resolve_System_FollowsTheOperatingSystem()
    {
        var selection = ThemeCatalog.Resolve("SYSTEM", BuiltInThemes.All);

        Assert.True(selection.FollowsSystem);
        Assert.Null(selection.Theme);
    }

    [Fact]
    public void Resolve_KnownId_ReturnsThatTheme()
    {
        var themes = ThemeCatalog.Merge(new ThemeLoadReport([User("medianoche")], [], [])).Themes;

        Assert.Equal("medianoche", ThemeCatalog.Resolve("Medianoche", themes).Theme!.Id);
        Assert.Equal(BuiltInThemes.Light, ThemeCatalog.Resolve("light", themes).Theme);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Resolve_NoId_IsDarkWithoutExplanation(string? id)
    {
        var selection = ThemeCatalog.Resolve(id, BuiltInThemes.All);

        Assert.Equal(BuiltInThemes.Dark, selection.Theme);
        Assert.Null(selection.FallbackReason);
    }

    [Fact]
    public void Resolve_UnknownId_FallsBackToDarkAndExplains()
    {
        var selection = ThemeCatalog.Resolve("borrado", BuiltInThemes.All);

        Assert.False(selection.FollowsSystem);
        Assert.Equal(BuiltInThemes.Dark, selection.Theme);
        Assert.Contains("borrado", selection.FallbackReason);
    }
}

public class AppSettingsThemeTests
{
    [Fact]
    public void Default_ThemeIsDark()
    {
        Assert.Equal("dark", AppSettings.Default.ThemeId);
    }
}
