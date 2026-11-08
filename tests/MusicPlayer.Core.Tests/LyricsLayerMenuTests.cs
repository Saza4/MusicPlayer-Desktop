using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

/// <summary>Menú de capas del modo inmersivo y renglón que se rellena (docs/specs/lyrics-layers.md, criterios 12-14 y 17).</summary>
public class LyricsLayerMenuTests
{
    private static LyricsDisplayOptions Opts(bool t = false, bool p = false, bool hideOriginal = false) =>
        new(ShowTranslation: t, ShowPronunciation: p, HideOriginal: hideOriginal);

    private static IReadOnlyList<LyricsLayerMenuItem> Build(
        LyricsDisplayOptions o, bool hasT = true, bool hasP = true, string? code = "es", string culture = "es-ES") =>
        LyricsLayerMenu.Build(o, hasT, hasP, code, culture);

    private static LyricsLayerMenuItem Row(IReadOnlyList<LyricsLayerMenuItem> items, LyricsRow row) =>
        Assert.Single(items, i => i.Row == row);

    // ---- Botón (criterio 12) ----

    [Theory]
    [InlineData(true, true, false, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, false, false, false)]   // sincronizada pero sin capas
    [InlineData(false, true, true, false)]    // Plain: sin botón
    public void Button_OnlyWithSyncedLyricsAndALayer(bool synced, bool hasT, bool hasP, bool visible) =>
        Assert.Equal(visible, LyricsLayerMenu.IsButtonVisible(synced, hasT, hasP));

    // ---- Opciones y orden (criterio 13) ----

    [Fact]
    public void BothLayers_ThreeOptionsInOrder()
    {
        var items = Build(Opts());

        Assert.Equal([LyricsRow.Translation, LyricsRow.Pronunciation, LyricsRow.Original], items.Select(i => i.Row));
        Assert.Equal(["Mostrar traducción", "Mostrar pronunciación", "Ocultar original"], items.Select(i => i.Label));
    }

    [Fact]
    public void OnlyPronunciation_NoTranslationOption()
    {
        var items = Build(Opts(), hasT: false);

        Assert.Equal([LyricsRow.Pronunciation, LyricsRow.Original], items.Select(i => i.Row));
    }

    [Fact]
    public void NoLayers_NoMenu() => Assert.Empty(Build(Opts(t: true, p: true), hasT: false, hasP: false));

    [Fact]
    public void LabelsFollowWhatIsShown()
    {
        var items = Build(Opts(t: true, p: true, hideOriginal: true));

        Assert.Equal(["Ocultar traducción", "Ocultar pronunciación", "Mostrar original"], items.Select(i => i.Label));
    }

    // Criterio 13: HideOriginal activo, pero el original se ve porque no queda otra capa visible.
    [Fact]
    public void OriginalShownByTheFallback_SaysHideAndIsDisabled()
    {
        var original = Row(Build(Opts(hideOriginal: true)), LyricsRow.Original);

        Assert.Equal("Ocultar original", original.Label);
        Assert.False(original.IsEnabled);
    }

    [Fact]
    public void PreferenceForAMissingLayer_DoesNotCountAsShown()
    {
        // Pide pronunciación, pero la canción no tiene: la traducción se puede ocultar, el original no (quedaría vacío).
        var items = Build(Opts(t: true, p: true, hideOriginal: true), hasP: false);

        Assert.Equal(["Ocultar traducción", "Mostrar original"], items.Select(i => i.Label));
        Assert.False(Row(items, LyricsRow.Translation).IsEnabled);
        Assert.True(Row(items, LyricsRow.Original).IsEnabled);
    }

    // ---- Desactivar lo que dejaría todo oculto (criterio 13) ----

    [Fact]
    public void HidingTheOnlyVisibleLayer_IsDisabled_EvenIfItIsNotTheOriginal()
    {
        var items = Build(Opts(t: true, hideOriginal: true));

        Assert.False(Row(items, LyricsRow.Translation).IsEnabled);    // «Ocultar traducción»: no quedaría nada
        Assert.True(Row(items, LyricsRow.Pronunciation).IsEnabled);   // «Mostrar pronunciación»
        Assert.True(Row(items, LyricsRow.Original).IsEnabled);        // «Mostrar original»
    }

    [Fact]
    public void WithTwoVisibleLayers_EitherCanBeHidden()
    {
        var items = Build(Opts(t: true, p: true, hideOriginal: true));

        Assert.True(Row(items, LyricsRow.Translation).IsEnabled);
        Assert.True(Row(items, LyricsRow.Pronunciation).IsEnabled);
    }

    [Fact]
    public void HidingTheOriginal_WithoutAnyOtherVisibleLayer_IsDisabled()
    {
        var original = Row(Build(Opts()), LyricsRow.Original);

        Assert.Equal("Ocultar original", original.Label);
        Assert.False(original.IsEnabled);
    }

    // ---- Resultado de pulsar (criterio 14: las mismas preferencias que el panel) ----

    [Fact]
    public void Result_TogglesOnlyItsOwnPreference()
    {
        var o = Opts(t: true, p: false, hideOriginal: false);
        var items = Build(o);

        Assert.Equal(o with { ShowTranslation = false }, Row(items, LyricsRow.Translation).Result);
        Assert.Equal(o with { ShowPronunciation = true }, Row(items, LyricsRow.Pronunciation).Result);
        Assert.Equal(o with { HideOriginal = true }, Row(items, LyricsRow.Original).Result);
    }

    [Fact]
    public void Result_ShowOriginal_ClearsHideOriginal()
    {
        var item = Row(Build(Opts(t: true, hideOriginal: true)), LyricsRow.Original);

        Assert.Equal("Mostrar original", item.Label);
        Assert.False(item.Result.HideOriginal);
    }

    [Fact]
    public void Result_KeepsEmphasizePronunciation()
    {
        var o = new LyricsDisplayOptions(ShowPronunciation: true, EmphasizePronunciation: true);

        Assert.True(Row(Build(o), LyricsRow.Translation).Result.EmphasizePronunciation);
    }

    // ---- Código del idioma en la etiqueta (criterio 13) ----

    [Theory]
    [InlineData("es", "es-ES", "Mostrar traducción")]
    [InlineData("es-419", "es-MX", "Mostrar traducción")]       // mismo idioma: sin código
    [InlineData("en", "es-ES", "Mostrar traducción (en)")]
    [InlineData("pt-BR", "es-ES", "Mostrar traducción (pt-BR)")]
    [InlineData(null, "es-ES", "Mostrar traducción")]           // no se sabe: sin código
    [InlineData("en", "", "Mostrar traducción (en)")]           // cultura invariante
    public void TranslationLabel_CarriesTheCodeWhenItIsNotTheSystemLanguage(string? code, string culture, string label) =>
        Assert.Equal(label, Row(Build(Opts(), code: code, culture: culture), LyricsRow.Translation).Label);

    // ---- Relleno (criterio 17) ----

    [Theory]
    [InlineData(true, true, true, LyricsRow.Original)]
    [InlineData(true, false, false, LyricsRow.Original)]
    [InlineData(false, true, true, LyricsRow.Pronunciation)]
    [InlineData(false, true, false, LyricsRow.Pronunciation)]
    [InlineData(false, false, true, LyricsRow.Translation)]
    [InlineData(false, false, false, LyricsRow.Original)]     // nada visible: no pasa, pero no rellena una fila oculta
    public void FillRow_TheOriginalIfShown_ElseTheFirstVisibleLayer(bool original, bool pronunciation, bool translation, LyricsRow row) =>
        Assert.Equal(row, LyricsLayerMenu.FillRow(original, pronunciation, translation));
}
