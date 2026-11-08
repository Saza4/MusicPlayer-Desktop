using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Services;

/// <summary>Una capa de la letra que el menú del modo inmersivo muestra u oculta.</summary>
public enum LyricsRow { Original, Pronunciation, Translation }

/// <summary>
/// Una opción del menú: su etiqueta («Ocultar traducción (en)»), si se puede pulsar y las preferencias que deja al
/// pulsarla.
/// </summary>
public sealed record LyricsLayerMenuItem(LyricsRow Row, string Label, bool IsEnabled, LyricsDisplayOptions Result);

/// <summary>
/// Menú de capas del modo inmersivo (docs/specs/lyrics-layers.md, criterios 12-14). Usa las mismas preferencias que
/// el panel lateral (<see cref="LyricsDisplayOptions"/>), así que los dos quedan sincronizados.
/// </summary>
public static class LyricsLayerMenu
{
    /// <summary>Criterio 12: el botón solo se ve con letra sincronizada y al menos una capa.</summary>
    public static bool IsButtonVisible(bool isSynced, bool hasTranslation, bool hasPronunciation) =>
        isSynced && (hasTranslation || hasPronunciation);

    /// <summary>
    /// Opciones del menú en orden: traducción, pronunciación y original (cada una solo si su capa existe; el original,
    /// si existe alguna otra). Las etiquetas siguen lo que se ve (<see cref="LyricsDisplayOptions.Resolve"/>), no la
    /// preferencia guardada. Se desactiva cualquier opción que dejaría todo oculto.
    /// </summary>
    /// <param name="translationLanguage">Código de la traducción mostrada («en»); null si no se sabe.</param>
    /// <param name="systemCulture">Cultura de la interfaz («es-MX»): con otro idioma, la etiqueta lleva el código.</param>
    public static IReadOnlyList<LyricsLayerMenuItem> Build(
        LyricsDisplayOptions options, bool hasTranslation, bool hasPronunciation,
        string? translationLanguage, string systemCulture)
    {
        if (!hasTranslation && !hasPronunciation) return [];

        var shown = options.Resolve(hasTranslation, hasPronunciation);
        var items = new List<LyricsLayerMenuItem>(3);

        if (hasTranslation)
        {
            var code = translationLanguage is { Length: > 0 } c && !LyricsLanguage.IsSystemLanguage(c, systemCulture)
                ? $" ({c})"
                : "";
            items.Add(Item(LyricsRow.Translation, shown.ShowTranslation, "traducción" + code,
                options with { ShowTranslation = !shown.ShowTranslation }, hasTranslation, hasPronunciation));
        }
        if (hasPronunciation)
            items.Add(Item(LyricsRow.Pronunciation, shown.ShowPronunciation, "pronunciación",
                options with { ShowPronunciation = !shown.ShowPronunciation }, hasTranslation, hasPronunciation));

        items.Add(Item(LyricsRow.Original, shown.ShowOriginal, "original",
            options with { HideOriginal = shown.ShowOriginal }, hasTranslation, hasPronunciation));

        return items;
    }

    private static LyricsLayerMenuItem Item(
        LyricsRow row, bool visible, string name, LyricsDisplayOptions result, bool hasTranslation, bool hasPronunciation) =>
        new(row, (visible ? "Ocultar " : "Mostrar ") + name, LeavesSomethingVisible(result, hasTranslation, hasPronunciation), result);

    // Sin contar el respaldo de Resolve (que vuelve a mostrar el original): si nada queda pedido, todo quedaría oculto.
    private static bool LeavesSomethingVisible(LyricsDisplayOptions o, bool hasTranslation, bool hasPronunciation) =>
        !o.HideOriginal || (o.ShowTranslation && hasTranslation) || (o.ShowPronunciation && hasPronunciation);

    /// <summary>
    /// Renglón que se rellena en la línea activa (criterio 17): el original si se ve; si no, la primera capa visible
    /// (pronunciación y, si no, traducción). Las demás se atenúan según el estado de la línea.
    /// </summary>
    public static LyricsRow FillRow(bool showOriginal, bool showPronunciation, bool showTranslation) =>
        showOriginal || (!showPronunciation && !showTranslation) ? LyricsRow.Original
        : showPronunciation ? LyricsRow.Pronunciation
        : LyricsRow.Translation;
}
