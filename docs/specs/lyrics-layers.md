# Especificación: Capas de letra (traducción y pronunciación incrustadas, y botón en el inmersivo)

Estado: **aprobada (7 de noviembre de 2026), en curso: M0 a M3 hechos; M4 hecho a falta de la prueba a mano del autor**. El autor aceptó P1-P6 con ajustes (ver «Revisión del autor»). Rama: `feature/capas-letra`.

## Contexto

Hoy la traducción y la pronunciación se leen solo de archivos hermanos junto al audio (`cancion.<idioma del sistema>.lrc` y `cancion.rom.lrc`, en `CompanionLyricsProvider`) y se unen al original con `LyricsMerger` (por marca de tiempo, ±300 ms). «Incrustar letra (.lrc)…» solo incrusta el principal (USLT en MP3, `LYRICS` en FLAC, `©lyr` en M4A) con `SafeFileEdit` y verificación. El README lo anota como limitación: las capas no viajan con el audio.

Dos cambios:
- **A.** Incrustar también las capas hermanas y leerlas del archivo cuando no haya hermanos.
- **B.** Un botón en el modo inmersivo con un `MenuFlyout` para mostrar u ocultar las capas, con el mismo estado persistente que el panel lateral (que no se toca).

Lo que ya existe y se reutiliza:
- `LyricsDisplayOptions.HideOriginal` (persistido en `AppSettings`) y `Resolve()`, que ya impide ocultarlo todo.
- `LyricsViewModel.HasTranslation` / `HasPronunciation`, calculados de las líneas unidas.
- `LyricsMerger.Merge`, `LyricsEditor` (UTF-8 estricto, 1 MB, no vacío), `SafeFileEdit.Run`.
- En la vista progresiva, el orden de renglones es pronunciación, original (relleno) y traducción. Hoy, con el original oculto, no se rellena nada.

## Criterios de aceptación

**Cambio A: incrustar**
1. «Incrustar letra (.lrc)…» incrusta en **una sola escritura atómica** (`SafeFileEdit`) el principal y las capas que existan junto a él.
   - Capas que cuentan: `<nombre>.<código>.lrc` como traducción y `<nombre>.rom.lrc` como pronunciación.
   - **Código de idioma válido:** un código ISO 639-1 real (lista fija en Core, sin depender de las culturas del sistema), con una subetiqueta opcional de región (`pt-BR`, dos letras, o `es-419`, tres cifras de la ONU) o de escritura (`zh-Hans`, cuatro letras). Se normaliza: idioma en minúsculas, región en mayúsculas y escritura con mayúscula inicial. `.alt.lrc`, `.mix.lrc` o `.rom.lrc` no son traducciones (`alt` y `mix` solo están en ISO 639-3; `rom` es la pronunciación).
   - **Los códigos de tres letras no cuentan como traducción nueva.** Única excepción, por compatibilidad: el código de idioma del propio sistema, que es lo que se lee hoy (ver «Compatibilidad con los nombres de hoy»).
   - Los hermanos no se borran ni se modifican; se comprueba con un hash en la prueba.
2. **Dónde se guarda cada capa:**
   - El principal sigue en su campo estándar, sin cambios: USLT, `LYRICS` y `©lyr`.
   - Traducción y pronunciación van en campos propios, con el mismo nombre en los tres formatos: `LYRICS_TRANSLATION_<CÓDIGO>` (por ejemplo `LYRICS_TRANSLATION_ES` o `LYRICS_TRANSLATION_PT-BR`) y `LYRICS_PRONUNCIATION`. **Se escriben en mayúsculas y se leen sin distinguir mayúsculas.**

     | Formato | Campo |
     |---|---|
     | MP3 | TXXX |
     | FLAC | comentario Vorbis |
     | M4A | átomo libre `----:com.apple.iTunes:…` |

   - La traducción lleva su código de idioma; la pronunciación, no.
3. **Verificación:** tras escribir, se reabre la copia y se comparan todas las capas escritas. Si una falla, el original queda intacto.
4. **Reincrustar:**
   - Reemplaza solo las capas que tienen archivo de origen; las demás se conservan.
   - Nunca duplica un campo.
5. **Validación por capa:** la misma de hoy (UTF-8 estricto, ≤ 1 MB por capa, no vacía).
   - Principal inválido: se cancela todo y no se escribe nada.
   - Capa secundaria inválida: se salta y se avisa.
   - **Capa de otro tipo que el principal** (añadido tras M4, a petición del autor): se salta y se avisa. Con un principal sincronizado, una capa sin marcas de tiempo se salta con «no tiene marcas de tiempo como el original»; con un principal sin marcas, una sincronizada, con «tiene marcas de tiempo y el original no»; con un principal sin letra (solo metadatos, que se sigue incrustando porque la validación del principal no la analiza), todas, con «el original no tiene letra que leer». Si los dos son sin marcas, se salta la capa con otro número de líneas, con «no tiene tantas líneas como el original», porque `LyricsMerger` empareja por posición y solo con el mismo número; con el mismo número sí se ve en el panel lateral y se incrusta. **Por qué:** al leer, `LyricsMerger` ignora una capa de otro tipo que el original; incrustarla dejaría en el archivo un campo muerto y la línea de estado diría que se incrustó algo que nunca se verá. El tipo se compara con el del principal que se está incrustando, en `LyricsEditor` (Core).
6. **Hermano sin principal:** sin `.lrc` principal no se incrusta. Si se elige el principal con el selector, las capas se toman junto al archivo elegido (`elegido.es.lrc`), no junto al audio. **En la práctica, con el selector casi solo se incrusta el principal**: rara vez hay capas junto a un `.lrc` elegido de otra carpeta.
7. **Línea de estado:** resume lo incrustado y lo saltado. Ejemplo: «Letra de "X" incrustada: original, traducción (es) y pronunciación · saltada: traducción (en), no está en UTF-8».

**Lectura**

8. Si la canción no tiene hermanos pero sí capas incrustadas, se muestran igual en el panel y en el inmersivo.
9. **Pronunciación:** el hermano gana al incrustado, la misma prioridad que el principal.
10. **Una sola traducción visible, elegida primero por idioma** entre todas las disponibles (hermanas e incrustadas juntas):
    1. La del idioma exacto del sistema (`CurrentUICulture.Name`, por ejemplo `pt-BR`).
    2. Si no, la del mismo idioma principal (`pt`): primero la que no lleva subetiqueta y luego la primera alfabética (`pt-BR` antes que `pt-PT`).
    3. Si no, la primera por orden alfabético de código.
    - **Dentro del mismo código, el hermano gana al incrustado.**
    - Cambia la conducta de hoy: un `.en.lrc` con el sistema en español pasa a mostrarse (hoy solo se lee el del idioma del sistema).
11. **El panel lateral cambia de comportamiento sin cambio de código:** como lee del mismo proveedor, mostrará también las capas incrustadas y la traducción en otro idioma. Su texto «Esta canción no tiene archivos .lrc de traducción ni de pronunciación.» deja de ser exacto con capas incrustadas; se deja como está y se anota como limitación (P5).

**Cambio B: botón en el inmersivo**

12. Un botón con icono vectorial de burbuja con caracteres (distinto del de comillas) abre un `MenuFlyout`.
    - Solo se muestra con letra **sincronizada** y al menos una capa disponible, sea hermana o incrustada.
13. **Opciones del menú:**
    - «Mostrar/Ocultar traducción», «Mostrar/Ocultar pronunciación» y «Mostrar/Ocultar original». Cada una solo aparece si su capa existe (el original, siempre que haya alguna otra).
    - **Las etiquetas siguen el estado resuelto por `Resolve()`**, no la preferencia guardada: si `HideOriginal` está activo pero el original se ve porque no queda otra capa visible, la etiqueta dice «Ocultar original».
    - **Se desactiva cualquier opción que dejaría todo oculto** (no solo «Ocultar original»): por ejemplo, «Ocultar traducción» con el original oculto y sin pronunciación visible.
    - Cuando la traducción mostrada no es la del idioma del sistema, la etiqueta lleva el código: «Mostrar traducción (en)».
    - El cálculo de etiquetas, visibilidad y desactivado vive en Core, con pruebas.
14. **Estado compartido:** el menú usa `LyricsViewModel.ShowTranslation`, `ShowPronunciation` y `HideOriginal`, el mismo estado persistente que el panel lateral. Así quedan sincronizados; el panel no se modifica.
15. **Colores** solo con recursos del tema, sin literales `#RRGGBB` en el XAML nuevo.
16. **Espacio** sigue siendo reproducir/pausa con el foco en el botón y el menú cerrado (se comprueba a mano). Con el menú abierto, las teclas son del menú: Espacio y Enter activan la opción enfocada y Esc cierra.
17. **Relleno:**
    - Con el original visible, solo se rellena el original.
    - Con el original oculto, se rellena la primera capa visible (pronunciación y, si no, traducción). El cálculo vive en Core, con prueba.
    - Las capas que no se rellenan se atenúan según el estado de la línea, como hoy.
    - El criterio 6 de `lyrics-progressive.md` se enmienda con referencia a esta spec.

**Calidad**

18. Lógica nueva en Core con pruebas, solo con letras sintéticas.
19. Pruebas de escritura y lectura en MP3, FLAC y M4A (el spike confirmó que M4A es fiable).
20. `dotnet build -c Release` sin warnings nuevos (hoy hay 1 CS8602 conocido). Todas las pruebas en verde y CI verde en Windows y Ubuntu.
21. Un `settings.json` anterior carga igual: no hay ajustes nuevos, y una prueba lo confirma.
22. **Archivo M4A de prueba:** excepción explícita a la regla de no subir audio. Es silencio de 0,1 s generado con ffmpeg (sin derechos), en `tests/MusicPlayer.Infrastructure.Tests/Fixtures/`, anotado en CLAUDE.md. El `.gitignore` no ignora audio, así que no hace falta negación.

## Fuera de alcance
- Lo que dio el usuario:
  - Capas generadas automáticamente o descargadas.
  - Pronunciación palabra a palabra.
  - Tamaño de texto por capa.
  - Editar capas en la app.
- Además:
  - Cambiar el código del panel lateral, incluido su texto «no tiene archivos .lrc…» (ver criterio 11).
  - Elegir entre varias traducciones desde la UI.
  - «Pronunciación en texto grande» en el menú del inmersivo.
  - Borrar capas incrustadas.
  - Formatos distintos de MP3, FLAC y M4A (OGG, Opus, WMA): solo el principal, como hoy, con aviso.

## Casos límite
- **Hermano sin principal:** no se incrusta nada y la línea de estado lo dice. Con el selector, ver P3.
- **Distinto número de líneas o marcas que no casan:**
  - Con letra sincronizada, se incrusta tal cual: no se valida contra el original (las líneas que casan se ven).
  - Con letra sin marcas, una capa con otro número de líneas se salta (criterio 5): no casaría ninguna.
  - Al leer, `LyricsMerger` empareja por marca (±300 ms); las líneas sin pareja quedan sin capa.
- **Hermano vacío, mal formado o no UTF-8:** se salta, se avisa y lo demás se incrusta.
  - «Mal formado» significa que no da letra al analizarlo.
  - Con un tipo distinto del principal (Plain frente a Synced) se salta al incrustar (criterio 5), porque `LyricsMerger` lo ignoraría al leer. Si ya está incrustada (por otro programa), al leer cede su sitio a otra capa del mismo código (ver «Ajustes tras M3»).
- **Solo traducción o solo pronunciación:** se incrusta la que haya, y el menú solo muestra esa opción.
- **Reincrustar:** sin duplicados.
  - Una capa sin hermano conserva la incrustada, aunque el principal haya cambiado.
- **Editar un hermano después de incrustar:** el hermano gana al leer (criterios 9 y 10). El archivo de audio solo cambia al volver a incrustar.
- **Letra `Plain`:** sin botón y sin cambios en el inmersivo, que sigue con el render clásico.
- **Cambio de pista con el menú abierto:** el menú se cierra al cambiar las capas disponibles. Si queda abierto, sus opciones se recalculan y nunca actúa sobre una capa que ya no existe.
- **Espacio con el menú abierto:** el menú recibe las teclas (criterio 16).
- **Códigos con región o escritura** (`pt-BR`, `zh-Hans`): valen como traducción si el idioma es ISO 639-1; `pt-BR` y `pt` son traducciones distintas. `es-419` (Latinoamérica, región numérica) también vale. Un código cuya subetiqueta no tenga esa forma (`es-latino`, `es-41`) no cuenta.
- **Códigos que no son idiomas** (`.alt.lrc`, `.mix.lrc`, `.live.lrc`): se ignoran.
- **`settings.json` anterior:** carga igual.
- **Varias traducciones** (`.es.lrc` y `.en.lrc`): se incrustan todas y se muestra una (criterio 10).
- **Audio con un punto en el nombre** (`tema.en vivo.mp3`): los hermanos son `tema.en vivo.es.lrc`. La regla toma el nombre sin extensión del audio como prefijo exacto.
- **Mismo código repetido** (`cancion.es.lrc` y `cancion.ES.lrc`, posible en Linux): se toma uno, el primero en orden ordinal del nombre.
- **Archivo en uso por la reproducción:** se libera antes de escribir con el evento `Saving` → `OnTagsSaving`, como hoy.

## Decisiones tomadas (del usuario)
1. El principal va en su campo estándar. Traducción y pronunciación van en TXXX, Vorbis y átomo libre. Antes, un spike. Si M4A no es fiable, primero MP3 y FLAC, con M4A como limitación.
2. Se guarda el código del archivo (`es`). Se muestra una traducción: la del sistema o la primera alfabética. La pronunciación no lleva idioma.
3. «Ocultar original» **ya existe** (`LyricsDisplayOptions.HideOriginal`) y se reutiliza. No se puede ocultar todo. Con el original oculto, se rellena la primera capa visible.
4. Sin diálogo: se incrusta todo lo que haya, en una sola escritura. Reincrustar reemplaza solo las capas con fuente. Un hermano sin principal no se incrusta. La línea de estado resume.
5. 1 MB por capa. Principal inválido: se cancela. Secundaria inválida: se salta y se avisa.

## Compatibilidad con los nombres de hoy

Comprobado antes de M1. Hoy `CompanionLyricsProvider` solo prueba **un** nombre de traducción: `<nombre>.<CultureInfo.CurrentUICulture.TwoLetterISOLanguageName>.lrc` (más `<nombre>.rom.lrc`). En Windows coincide sin distinguir mayúsculas (`cancion.ES.lrc` vale); en Linux, solo en minúsculas.

| Hoy | Cuántos | Con la regla nueva |
|---|---|---|
| Código ISO 639-1 del sistema (`es`, `pt`, `zh`…) | todos los de dos letras de .NET están en la lista de 183, salvo `iv` | se sigue mostrando |
| Código de tres letras del sistema, en culturas sin ISO 639-1 (`fil`, `haw`, `chr`… en .NET 8 son 101) | uno: el del sistema | **se sigue mostrando** por la excepción de compatibilidad: el código del sistema vale aunque no esté en la lista |
| `iv` (cultura invariante, por ejemplo en Linux sin ICU) | uno | se sigue aceptando por la misma excepción (inofensivo) |
| Con región (`es-MX`, `pt-BR`) | **nunca** se leía: con el sistema en `es-MX` se busca `.es.lrc` | pasa a contar como traducción (nuevo) |
| Otros idiomas (`.en.lrc` con el sistema en español) | nunca se leían | pasan a contar (criterio 10) |

Así, ningún nombre que hoy se muestra deja de mostrarse. Una prueba lo fija para `.es.lrc`, `.ES.lrc` (Windows), `.fil.lrc` con el sistema en `fil` y `.es-MX.lrc`. Los códigos de tres letras **distintos** del del sistema no cuentan como traducción.

## Revisión del autor (P1-P6 aceptados, con ajustes)
- **P1. Nombres de campo:** `LYRICS_TRANSLATION_<CÓDIGO>` y `LYRICS_PRONUNCIATION` en los tres formatos, escritos en mayúsculas y leídos sin distinguir mayúsculas (criterio 2). TXXX y no USLT adicionales: otro reproductor nunca muestra la traducción como letra principal, y `Tag.Lyrics = null` no las borra (comprobado en M0).
- **P2. Traducción en otro idioma:** la regla vale igual para hermanos e incrustadas. Se elige primero por idioma y, dentro del mismo, gana el hermano (criterio 10). El menú muestra el código cuando no es la del sistema (criterio 13).
- **P3. Selector:** las capas se toman junto al archivo elegido; casi siempre se incrusta solo el principal (criterio 6).
- **P4. Espacio:** con el menú abierto, las teclas son del menú; con el menú cerrado, Espacio en el botón reproduce o pausa, comprobado a mano (criterio 16).
- **P5. Panel lateral:** su código no se toca, pero cambia su comportamiento; se anota (criterio 11).
- **P6. Archivos de prueba:** el FLAC se arma en la prueba; el M4A es un archivo guardado, como excepción explícita (criterio 22).
- **Código de idioma:** se valida contra una lista real ISO 639-1, con región o escritura opcionales (criterio 1).
- **Criterio 13:** se desactiva cualquier opción que dejaría todo oculto, y las etiquetas siguen `Resolve()`.
- **M4:** la prueba a mano se hace en tres copias, no en archivos reales.

## Hitos (un commit cada uno)

| Hito | Contenido | Verificación |
|---|---|---|
| M0 | (**hecho**, ver «Resultado del spike») Spike de TagLib 2.3.0: escribir y leer TXXX, Vorbis y `----` con texto multilínea y Unicode, ~1 MB y reabriendo el archivo. Archivos de prueba FLAC/M4A (P6). Resultado anotado en la spec; la spec entra en el commit. | Pruebas del spike en Infrastructure |
| M1 | (**hecho**, ver «Resultado de M1») Core: modelo de capas (`LyricsLayers`: principal, traducciones por código, pronunciación), regla de nombres de hermanos (`<nombre>.<código>.lrc`) y de campos, elección de traducción. `LyricsEditor` valida por capa y devuelve un resumen (incrustado y saltado con su motivo). | Pruebas Core con un escritor falso |
| M2 | (**hecho**, ver «Resultado de M2») Infrastructure (escritura): `ILyricsWriter` con capas, `TagLibLyricsWriter` por formato, reemplazo solo de las capas con fuente, verificación de todas. | MP3, FLAC y M4A: escribe, reemplaza sin duplicar, conserva etiquetas y audio, no deja temporales, falla sin tocar el original. Además (pedido por el autor): nombres con guion (`LYRICS_TRANSLATION_PT-BR`, `LYRICS_TRANSLATION_ZH-HANS`) en TXXX, Vorbis y `----`, con la mayúscula normalizada de vuelta (`pt-BR`, `zh-Hans`); borrar la principal (`Tag.Lyrics = null`) no toca las capas en FLAC y M4A; el editor de etiquetas y «Limpiar etiquetas» (ambos con `TagLibTrackTagWriter`) conservan las capas en los tres formatos; una capa de exactamente 1 MB se escribe y se relee en los tres |
| M3 | (**hecho**, ver «Resultado de M3») Infrastructure (lectura): lector de capas incrustadas y prioridad por capa en `CompanionLyricsProvider` (hermano > incrustado; la traducción se elige por idioma entre hermanas e incrustadas juntas). La lectura de hermanos con elección por idioma ya estaba hecha (ajustes tras M1, antes de M2). | Pruebas en MP3, FLAC y M4A: solo incrustado, solo hermano, ambos, varias traducciones. Además (pedido por el autor): poner y quitar la carátula, y aplicarla a todo un álbum (`CoverEditor.SetManyAsync`), conservan las capas en los tres formatos |
| M4 | (**código hecho**, ver «Resultado de M4»; falta la prueba a mano) Desktop A: `EmbedLyricsViewModel` busca los hermanos (junto al audio o al elegido), aviso en la línea de estado. | A mano (autor), **en tres copias** (MP3, FLAC y M4A), no en archivos reales: la escritura no se puede deshacer. Incrustar y abrir cada copia sin los hermanos |
| M5 | Desktop B: botón e icono, `MenuFlyout` con etiquetas dinámicas, visibilidad, cierre al cambiar de pista, Espacio. Relleno de la primera capa visible (Core + `ProgressiveLyricsView`). | Pruebas Core del menú y del relleno; a mano: menú, sincronía con el panel, Plain sin botón, Espacio con el foco en el botón |
| M6 | Docs en commit aparte: README (limitación, y qué pasa con las capas al «Convertir formato» con ffmpeg: lo comprueba el autor a mano una vez), `reporte-reproductor.md`, spec, CLAUDE.md (número de pruebas). Push, PR y CI verde en Windows y Ubuntu. | CI |

## Resultado del spike (M0)

Con TagLibSharp 2.3.0, primero en el scratchpad y luego como pruebas (`TagLibLyricsLayersSpikeTests`, en Infrastructure):

| Formato | API | Multilínea, Unicode, `/` y `;` | ~740 KB (spike) y ~500 KB (prueba) | Reescribir | Mayúsculas al leer | Principal |
|---|---|---|---|---|---|---|
| MP3 (ID3v2.3, UTF-16) | `UserTextInformationFrame.Get(tag, desc, StringType.UTF16, true)` | igual, en una sola parte (no parte por `/`) | igual | no duplica | `Get` **distingue**: se busca recorriendo `GetFrames<UserTextInformationFrame>()` con `OrdinalIgnoreCase` | USLT intacto; `Tag.Lyrics = null` no toca los TXXX |
| FLAC (Vorbis) | `XiphComment.SetField`, `GetFirstField` y enumeración de nombres | igual | igual | no duplica | no distingue (Vorbis guarda los nombres en mayúsculas) | `LYRICS` intacto |
| M4A (`----`) | `AppleTag.SetDashBox("com.apple.iTunes", nombre, texto)` y `GetDashBox`; los nombres se enumeran con las cajas `----` y sus hijas `mean` y `name` | igual | igual | no duplica | `GetDashBox` no distingue | `©lyr` intacto |

- **M4A es fiable:** entra en el alcance y no queda como limitación.
- **FLAC de prueba armado a mano** (`fLaC` + STREAMINFO de 34 bytes, sin marcos de audio): TagLib lo abre, lo escribe y lo relee. No hace falta un archivo guardado.
- **M4A de prueba:** no se arma a mano. `Fixtures/silencio.m4a` (778 bytes, AAC, 8 kHz, mono, 0,1 s, sin metadatos): `ffmpeg -f lavfi -i anullsrc=r=8000:cl=mono -t 0.1 -c:a aac -b:a 16k -map_metadata -1 -fflags +bitexact -flags:a +bitexact silencio.m4a`.
- La escritura real (`TagLibLyricsWriter` con capas) es M2; las pruebas de M0 solo fijan lo que TagLib permite.

## Resultado de M1

Todo en Core, con pruebas (569 en Core, +87):
- **`LyricsLayers.cs`** (modelos): `LyricsLayerKind`, `LyricsLayerSource` (tipo, código y ruta; `Label` para la línea de estado), `LyricsLayer` (texto validado), `LyricsLayers` (principal y capas de una escritura), `LyricsLayerSkip` y `LyricsEmbedReport` con `Describe`, que da el texto de la línea de estado (sin capas, el mismo de hoy).
- **`LyricsLanguage`**: lista fija ISO 639-1 (183 códigos), `Normalize` (región de dos letras o de tres cifras, o escritura de cuatro letras; `es-latino` no vale; `es-419` se añadió después de M1, a petición del autor), `FromSuffix` (con la excepción de compatibilidad del código del sistema), `FromField` (lee lo que se escribió, incluido un código del sistema de tres letras), `Pick` (criterio 10) e `IsSystemLanguage` (si la etiqueta del menú lleva código).
- **`LyricsLayerNames`**: `FieldName` en mayúsculas, `TryParseField` sin distinguir mayúsculas y con el código normalizado, `FindSiblings` (prefijo exacto del nombre sin extensión, sin distinguir mayúsculas; ignora `.alt`, `.mix`, `.live`, `.fil` ajeno al sistema, `.es-latino` y los nombres con más puntos).
- **`LyricsEditor`** recibe ahora también `ILyricParser` y tiene `EmbedAsync(pista, principal, capas)`: valida cada capa por separado (UTF-8 estricto, ≤ 1 MB, no vacía, y que el analizador saque letra), salta las inválidas con su motivo y escribe todo en **una sola llamada** a `ILyricsWriter`. El principal inválido lanza como hoy y no escribe nada. Una capa de exactamente 1 MB se acepta. `EmbedAsync(pista, principal)` sigue igual, sin capas.
- **`ILyricsWriter.Write(ruta, LyricsLayers)`**: firma nueva. Hasta M2, `TagLibLyricsWriter` escribe solo el principal y lanza `NotSupportedException` si llegan capas (nadie las pasa todavía), en lugar de perderlas sin avisar.

**Ajustes tras M1 (pedidos por el autor):**
- **`es-419`:** se acepta una región numérica de tres cifras (regiones de la ONU). Siguen sin valer `es-latino`, `es-41`, `alt`, `mix` y `xx`.
- **Linux:** la lectura de hermanos y la incrustación usan **el mismo buscador**, `LyricsSiblingFiles.Find` (Infrastructure). Recorre la carpeta con `MatchCasing.CaseInsensitive` (en Linux el patrón `*.lrc` distingue mayúsculas por defecto) y compara los nombres con `LyricsLayerNames.FindSiblings`, sin distinguir mayúsculas. No hay ninguna consulta por nombre exacto.
- **Parte de M3 adelantada:** `CompanionLyricsProvider` ya usa ese buscador y elige la traducción con `LyricsLanguage.Pick`, con la cultura del sistema (`CultureInfo.CurrentUICulture.Name`). Pruebas: otro idioma, varias traducciones, `es-ES` frente a `es-419`, `.ES.lrc` y `SONG.ROM.LRC`, `.fil.lrc` con el sistema en `fil`, y `.fil`, `.alt` y `.mix` ignorados. Falta la lectura de lo incrustado (M3).
- **El principal no cambia:** `cancion.lrc` se sigue buscando por nombre exacto (`Path.ChangeExtension`), igual en la lectura (`LrcFileLyricsProvider`) y en la incrustación (`EmbedLyricsViewModel`). Las dos partes ven lo mismo; en Linux, `cancion.LRC` no cuenta como principal, igual que hoy.
- **CI tras los ajustes:** PR en borrador #5, abierto solo para ver Ubuntu (el CI corre en pull requests). Verde en `windows-latest` y `ubuntu-24.04`, con las pruebas de `.ES.lrc` y `SONG.ROM.LRC`.

## Resultado de M2

- **`TagLibLyricsLayerFields`** (Infrastructure): `Supports` (`.mp3`, `.flac` y `.m4a`, sin distinguir mayúsculas), `Set`, `Get` y `Read` por formato. `Set` quita antes cualquier campo con el mismo nombre en otras mayúsculas (TXXX con `RemoveFrame`, Vorbis con `RemoveField`, `----` con `SetDashBox(…, null)`) y escribe el nombre en mayúsculas. `Read` devuelve las capas con el código normalizado e ignora los campos que no son capas o tienen un código que no vale. Lo usará la lectura de M3.
- **`TagLibLyricsWriter`**: principal y capas en la misma copia temporal de `SafeFileEdit` y verificación de todas al reabrir. Las capas que no vienen se conservan. Con capas en un formato que no las guarda, lanza `NotSupportedException`.
- **`ILyricsWriter.SupportsLayers`**: el editor la consulta antes. En un formato sin capas (WAV), incrusta solo el principal y salta las capas con el motivo «este formato no la guarda» (spec, «Fuera de alcance»).
- **Pruebas** (Infrastructure: 114, +36; Core: 577, +1), cada una en MP3, FLAC y M4A:
  - Principal y capas vuelven iguales, con los nombres en mayúsculas en el archivo.
  - **Nombres con guion:** `LYRICS_TRANSLATION_PT-BR`, `LYRICS_TRANSLATION_ZH-HANS` y `LYRICS_TRANSLATION_ES-419` en TXXX, Vorbis y `----`; al leer vuelven `pt-BR`, `zh-Hans` y `es-419`.
  - Un campo escrito en minúsculas por otro programa se lee y, al reincrustar, se reemplaza sin duplicar.
  - Reincrustar reemplaza solo las capas con fuente, no duplica nada y conserva las demás; incrustar solo el principal conserva las capas.
  - **(a)** Borrar la letra principal (`Tag.Lyrics = null`) no toca las capas.
  - **(b)** El editor de etiquetas y «Limpiar etiquetas» (los dos escriben con `TagLibTrackTagWriter`, a través de `TagEditor`) conservan las capas.
  - **(c)** Una capa de **exactamente 1 MB** (1 048 576 bytes en UTF-8) se escribe y se relee.
  - Se conservan las etiquetas de texto y el audio, y no quedan temporales. Un archivo dañado falla sin tocar el original.
  - En FLAC, TagLib da todo el archivo como «invariante», así que la prueba compara el STREAMINFO y un marco de audio inventado al final del FLAC de prueba.

## Resultado de M3

- **`ILyricsLayerReader`** (Core) y **`TagLibLyricsLayerReader`** (Infrastructure, con `TagLibLyricsLayerFields.Read`): devuelve las capas incrustadas con el código normalizado. Vacío en formatos sin capas (WAV, OGG…), si el archivo no existe o está dañado; nunca lanza por el archivo.
- **`CompanionLyricsProvider`** recibe el lector (registrado en `CompositionRoot`) y elige cada capa:
  - **Pronunciación:** el hermano y, si no, la incrustada (criterio 9).
  - **Traducción:** `LyricsLanguage.Pick` sobre los códigos de hermanas e incrustadas juntos; con el código elegido, el hermano y, si no, la incrustada (criterio 10).
  - **Hermano sin letra:** un hermano que no se puede leer o no da letra (vacío o mal formado) cede su sitio a la incrustada del mismo tipo y código, en lugar de dejar la capa vacía.
  - La principal puede venir de `cancion.lrc` y las capas del audio, o al revés: cada capa se resuelve por separado.
- **Pruebas** (Infrastructure: 146, +32; Core: 577, sin cambios), con archivos escritos por `TagLibLyricsWriter` y la misma cadena de proveedores que `CompositionRoot`, en MP3, FLAC y M4A:
  - Solo incrustado (criterio 8, también la principal incrustada), solo hermano y ambos (gana el hermano); cada capa de un sitio distinto; principal de `cancion.lrc` con capas del audio; sin capas, el original tal cual.
  - Varias traducciones incrustadas: la del sistema, la del mismo idioma (`pt-AO` → `pt-BR` antes que `pt-PT`) y, si no, la primera alfabética. Mezcla de hermanas e incrustadas: se elige primero el idioma (una `es` incrustada gana a una `en` hermana con el sistema en español).
  - Hermano vacío: se usa la incrustada. Archivo dañado, inexistente o WAV: el lector da vacío y las capas hermanas se siguen mostrando.
  - **Carátula (pedido del autor):** poner y quitar la carátula con `TagLibCoverArtWriter`, y aplicarla a un álbum de tres pistas (MP3, FLAC y M4A) con `CoverEditor.SetManyAsync`, conservan la principal y las capas.

**Ajustes tras M3 (revisión del autor):**
- **Coherencia con la principal, comprobada:** un `cancion.lrc` vacío, solo con espacios o con metadatos (`[ar:…]`), o que no se puede leer, da `LyricBlock.Empty` y `CompositeLyricsProvider` cae a la letra incrustada. Las capas hacen lo mismo. Prueba: `AMainLrcWithoutLyrics_FallsBackToTheEmbeddedMain`.
- **Hermano de otro tipo:** `LrcLyricParser` toma como `Plain` cualquier texto sin marcas, así que un hermano «mal formado» casi siempre da letra, pero `Plain`. Con un original `Synced`, `LyricsMerger` lo descartaba y la incrustada tampoco se usaba. Ahora una capa (hermana o incrustada) solo cuenta si es del mismo tipo que el original; si no, cede su sitio. La principal no tiene con qué compararse: un `cancion.lrc` con texto sin marcas se sigue mostrando como letra `Plain`, igual que hoy.
- Pruebas: Infrastructure 151 (+5).

## Resultado de M4

- **`ILyricsSiblingFinder`** (Core) y **`LyricsSiblingFinder`** (Infrastructure, sobre `LyricsSiblingFiles.Find` con el idioma corto del sistema), registrado en `CompositionRoot`. Así `EmbedLyricsViewModel` no depende de Infrastructure y ve los mismos hermanos que la lectura.
- **`EmbedLyricsViewModel`**: busca las capas junto al principal (`cancion.lrc` junto al audio o el archivo elegido con el selector, criterio 6), llama a `LyricsEditor.EmbedAsync(pista, principal, capas)` y pone en la línea de estado `LyricsEmbedReport.Describe`. Sin capas, el texto es el de siempre.
- **`LyricsEmbedReport.Describe`** ordena por su cuenta: traducciones por código y luego la pronunciación, en lo incrustado y en lo saltado. Antes seguía el orden de llegada, y `FindSiblings` da primero la pronunciación («original, pronunciación y traducción (es)», al revés que el criterio 7).
- **Pruebas** (Infrastructure: 158, +7; Core: 578, +1). `EmbedLyricsRoundTripTests`, en MP3, FLAC y M4A, hace sin la interfaz lo que hará el autor a mano: buscar los hermanos, incrustar, borrarlos y leer con la cadena de `CompositionRoot` (las capas salen del audio); un hermano no UTF-8 y otro vacío se saltan con su motivo en la línea de estado; con un principal elegido en otra carpeta, las capas se toman junto a él.
- **Para la prueba a mano 3 (hermano inválido):** se salta un hermano vacío, solo con metadatos, que no esté en UTF-8, que pese más de 1 MB o, tras el ajuste de abajo, con texto sin marcas de tiempo junto a un principal sincronizado.

**Ajuste tras M4 (pedido por el autor):** `LyricsEditor` compara el tipo de cada capa con el del principal y salta las de otro tipo con su motivo (criterio 5). Pruebas en Core (581, +3): principal sincronizado con una traducción y una pronunciación sin marcas (se saltan y la línea de estado lo dice), principal sin marcas con una capa sin marcas (se incrusta) y otra sincronizada (se salta), y principal solo con metadatos (se saltan todas).

**Revisión del ajuste (autor):**
- **«Principal solo con metadatos» es alcanzable:** desde M1, la validación del principal solo exige que exista, pese ≤ 1 MB, esté en UTF-8 y no esté vacío; no lo analiza. `[ti:Solo cabecera]` se incrusta, y la prueba `EmbedWithLayers_MainWithoutLyrics_SkipsEveryLayer` lo fija (el escritor recibe ese principal). La rama se queda.
- **Principal y capa sin marcas:** sí se ven en el panel lateral, pero solo con el mismo número de líneas (sin contar las vacías). Con otro número se saltan. Prueba: `EmbedWithLayers_PlainMain_SkipsAPlainLayerWithAnotherLineCount` (Core: 582, +1).
