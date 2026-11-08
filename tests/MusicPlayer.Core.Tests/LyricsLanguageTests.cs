using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

public class LyricsLanguageTests
{
    [Fact]
    public void TheListHasEveryIso6391Code() => Assert.Equal(183, LyricsLanguage.KnownLanguageCount);

    [Theory]
    [InlineData("es", "es")]
    [InlineData("ES", "es")]
    [InlineData("pt-BR", "pt-BR")]
    [InlineData("pt-br", "pt-BR")]
    [InlineData("PT-BR", "pt-BR")]
    [InlineData("es-MX", "es-MX")]
    [InlineData("zh-Hans", "zh-Hans")]
    [InlineData("ZH-HANS", "zh-Hans")]
    [InlineData("zh-hant", "zh-Hant")]
    [InlineData("es-419", "es-419")]   // región numérica de la ONU (Latinoamérica)
    [InlineData("ES-419", "es-419")]
    public void Normalize_ValidCodes(string raw, string expected) => Assert.Equal(expected, LyricsLanguage.Normalize(raw));

    [Theory]
    [InlineData("")]
    [InlineData("alt")]       // ISO 639-3 (altái meridional): no es traducción
    [InlineData("mix")]       // ISO 639-3
    [InlineData("rom")]
    [InlineData("live")]
    [InlineData("xx")]        // dos letras que no son un idioma
    [InlineData("iv")]        // la cultura invariante de .NET no es ISO 639-1
    [InlineData("fil")]       // tres letras: no cuenta como traducción nueva
    [InlineData("es-latino")] // subetiqueta que no es región ni escritura
    [InlineData("es-41")]     // región numérica: solo de tres cifras
    [InlineData("es-4190")]
    [InlineData("es-41a")]
    [InlineData("zh-Hans-CN")]
    [InlineData("es-")]
    public void Normalize_RejectsWhatIsNotIso6391(string raw) => Assert.Null(LyricsLanguage.Normalize(raw));

    // Compatibilidad (spec, «Compatibilidad con los nombres de hoy»): lo que hoy se muestra se sigue mostrando.
    [Theory]
    [InlineData("es", "es", "es")]
    [InlineData("ES", "es", "es")]     // Windows no distingue mayúsculas en el nombre
    [InlineData("fil", "fil", "fil")]  // cultura sin ISO 639-1: su código de tres letras sigue valiendo
    [InlineData("FIL", "fil", "fil")]
    [InlineData("iv", "iv", "iv")]     // cultura invariante: inofensivo
    [InlineData("en", "es", "en")]     // nuevo: otro idioma
    [InlineData("es-MX", "es", "es-MX")] // nuevo: con región
    public void FromSuffix_AcceptsTodaysNamesAndTheNewOnes(string suffix, string system, string expected) =>
        Assert.Equal(expected, LyricsLanguage.FromSuffix(suffix, system));

    [Theory]
    [InlineData("fil", "es")]  // tres letras distintas del sistema: no
    [InlineData("haw", "fil")]
    [InlineData("rom", "rom")] // la pronunciación nunca es traducción
    [InlineData("alt", "es")]
    [InlineData("mix", "")]
    public void FromSuffix_Rejects(string suffix, string system) => Assert.Null(LyricsLanguage.FromSuffix(suffix, system));

    [Theory]
    [InlineData("ES", "es")]
    [InlineData("PT-BR", "pt-BR")]
    [InlineData("ZH-HANS", "zh-Hans")]
    [InlineData("ES-419", "es-419")]
    [InlineData("FIL", "fil")] // se escribió como código del sistema
    public void FromField_NormalizesTheUppercaseName(string raw, string expected) =>
        Assert.Equal(expected, LyricsLanguage.FromField(raw));

    [Theory]
    [InlineData("")]
    [InlineData("ROM")]
    [InlineData("LIVE")]
    [InlineData("ES-LATINO")]
    public void FromField_Rejects(string raw) => Assert.Null(LyricsLanguage.FromField(raw));

    [Theory]
    [InlineData("pt-BR", "pt-BR", "en,pt,pt-BR,pt-PT")] // 1. idioma exacto
    [InlineData("pt", "pt-BR", "en,pt,pt-PT")]          // 2. mismo idioma: sin subetiqueta primero
    [InlineData("pt-BR", "pt-AO", "en,pt-PT,pt-BR")]    //    y luego alfabético
    [InlineData("es", "es-MX", "en,es,fr")]
    [InlineData("es-419", "es-MX", "en,es-ES,es-419")]   // mismo idioma, sin «es»: alfabético (las cifras van antes)
    [InlineData("es-419", "es-419", "es,es-419")]
    [InlineData("es-ES", "es-MX", "en,es-ES")]
    [InlineData("en", "es-MX", "fr,en")]                // 3. si no, el primero alfabético
    [InlineData("de", "", "fr,en,de")]                  // sin cultura
    [InlineData("fil", "fil-PH", "en,fil")]
    public void Pick_ChoosesByLanguageFirst(string expected, string culture, string codes) =>
        Assert.Equal(expected, LyricsLanguage.Pick(codes.Split(','), culture));

    [Fact]
    public void Pick_NoCodes_ReturnsNull() => Assert.Null(LyricsLanguage.Pick([], "es-ES"));

    [Theory]
    [InlineData("es", "es-MX", true)]
    [InlineData("es-AR", "es-MX", true)]
    [InlineData("en", "es-MX", false)]
    [InlineData("es", "", false)]
    public void IsSystemLanguage_ComparesThePrimaryLanguage(string code, string culture, bool expected) =>
        Assert.Equal(expected, LyricsLanguage.IsSystemLanguage(code, culture));
}
