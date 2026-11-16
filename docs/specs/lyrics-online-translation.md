# Spec: traducir la letra con un traductor en línea (opt-in)

Estado: **aprobada por el autor el 15 de noviembre de 2026**. Sin código todavía. Depende de `settings-panel.md`.

## Context

Feedback de uso (noviembre de 2026): «las letras […] mínimo debió haber la opción de traducir las letras con
algún traductor online».

La app no usa la red en ningún sitio. Lo dicen el README («No usa servicios de streaming ni la red»), el reporte
(«100 % local») y dos specs que dejaron esto fuera de alcance: `lyrics-layers.md` («capas generadas
automáticamente o descargadas») y `lyrics-progressive.md` («descarga de letras o servicios externos»).

**Decisión del autor (15 de noviembre de 2026):** se hace una excepción, opt-in. La función viene desactivada;
quien la activa acepta que el texto de la letra sale a un servicio externo. Esta spec sustituye esas dos líneas de
«fuera de alcance» solo para la traducción; descargar letras sigue fuera.

Lo que ya existe y se aprovecha: una traducción es un hermano `cancion.<idioma>.lrc` junto al audio, que
`CompanionLyricsProvider` ya lee, elige por idioma y une a la letra original (`LyricsMerger`, ±300 ms). Lo que
falta: nada en la app escribe un `.lrc`, y no hay ninguna interfaz de traductor.

## FEATURE: «Traducir en línea…»

### ACCEPTANCE CRITERIA

**Opt-in y privacidad**

1. Ajustes nuevos, **al final** de `AppSettings`: `OnlineTranslation = false` y `TranslationLanguage = ""`
   (vacío significa el idioma del sistema). En la sección «Letras» de Configuración: la casilla «Permitir
   traducir letras en línea» y una lista de idiomas de destino.
2. Junto a la casilla, un texto fijo dice qué sale del equipo y adónde: solo el texto de la letra (ni el archivo,
   ni el título, ni el artista, ni datos del usuario) al servicio elegido en M0, con su nombre.
3. Con el ajuste desactivado, la app no hace ninguna petición de red y las entradas del criterio 5 no aparecen.
   Ningún otro camino de la app usa la red.
4. La app solo habla con el servicio por HTTPS y no envía cabeceras que identifiquen al usuario ni al equipo
   (agente de usuario genérico).

**Uso**

5. Con el ajuste activado y una letra cargada, hay una acción «Traducir en línea…» en dos sitios: el menú de capas
   del modo inmersivo y el flyout «Traducción y pronunciación» del panel lateral.
6. Al usarla, la app traduce el texto de cada línea de la letra original al idioma de destino y guarda el
   resultado como `cancion.<idioma>.lrc` junto al audio, con las mismas marcas de tiempo que el original. Después
   recarga la letra y activa «Mostrar traducción».
7. Si ya existe un hermano con ese nombre, no se sobrescribe y la línea de estado lo dice. El archivo de audio no
   se toca, así que la reproducción no se interrumpe.
8. Mientras traduce, la línea de estado lo indica y la acción queda desactivada. Cambiar de pista cancela la
   traducción en curso sin dejar archivos a medias.
9. Cualquier fallo (sin conexión, tiempo agotado, límite del servicio, respuesta no válida, carpeta de solo
   lectura) termina con un mensaje en español en la línea de estado y sin archivo nuevo. La reproducción y la
   letra original no se ven afectadas.

**Robustez (la respuesta es una entrada no confiable)**

10. Se envían solo las líneas con texto, sin repetir las iguales, troceadas por el tope de caracteres por petición
    del servicio. Tope total: el de 1 MB que ya vale para una letra (`LyricsEditor.MaxBytes`).
11. De la respuesta se quitan saltos de línea y caracteres de control, y se neutraliza cualquier `[..]` o `<..>`
    que el parser pudiera leer como marca de tiempo. Cada línea traducida se limita a una longitud máxima.
12. Si el número de líneas devueltas no coincide con las enviadas, o una viene vacía, la traducción se rechaza
    entera.
13. Hay un tope propio de peticiones por sesión de la app (constante en Core, con prueba), para no depender solo
    de los límites del servicio; al alcanzarlo, la acción avisa y no envía nada.
13b. Cada petición tiene tiempo límite y tope de tamaño de respuesta; se puede cancelar.
14. El `.lrc` generado, leído con `LrcLyricParser`, da las mismas marcas que el original y los textos traducidos:
    prueba de ida y vuelta en Core.

**Arquitectura y pruebas**

15. `ILyricsTranslator` en Core; la preparación del envío, el saneado y la reconstrucción del `.lrc`
    (`LyricsTranslation`) en Core, con pruebas. La implementación HTTP (`HttpLyricsTranslator`) en Infrastructure,
    con `HttpClient` del propio .NET: Core sigue sin paquetes y no se añade ninguno a la solución.
16. Ninguna prueba usa la red: las de Infrastructure usan un `HttpMessageHandler` falso (respuesta correcta,
    error HTTP, tiempo agotado, respuesta enorme, JSON mal formado).
17. El traductor se registra en `CompositionRoot.cs`.
18. README, reporte y CLAUDE.md dejan de decir que la app no usa la red sin matices: dicen «local, salvo la
    traducción de letras en línea, que es opcional y viene desactivada». CLAUDE.md gana una regla: la red solo en
    el traductor y solo con el ajuste activado.
19. `dotnet build` sin advertencias nuevas y `dotnet test` en verde.

### OUT OF SCOPE

- Descargar letras, carátulas o metadatos.
- Traducir la pronunciación o generar romanización.
- Varios servicios a elegir, claves de API del usuario o un servidor propio.
- Incrustar la traducción en el audio en el mismo paso (ya existe «Incrustar traducción…» para el `.lrc`
  generado).
- Traducir toda la biblioteca de una vez.
- Sobrescribir o editar una traducción existente.
- Garantizar la calidad de la traducción.

### EDGE CASES

- **Letra sin tiempos (`Plain`):** se traduce igual; el hermano tiene el mismo número de líneas, que es lo que
  `LyricsMerger` exige para unirlo. La acción solo está en el panel lateral (el inmersivo no muestra el botón de
  capas con letra sin tiempos).
- **Letra incrustada, sin `cancion.lrc`:** se usa la letra cargada; el hermano de traducción se crea igual.
- **`.lrc` enhanced:** se envía el texto limpio de cada línea; la traducción no lleva marcas por palabra.
- **Líneas vacías y pausas:** no se envían; en el resultado quedan vacías con su marca.
- **La letra ya está en el idioma de destino:** el servicio devuelve lo mismo; se guarda igual (no se intenta
  detectar).
- **Ya hay una traducción a otro idioma:** se crea la nueva; cuál se muestra lo decide la regla de siempre
  (`LyricsLanguage.Pick`).
- **Idioma de destino que el servicio no admite:** mensaje en la línea de estado, sin archivo.
- **Carpeta de solo lectura o unidad desconectada:** mensaje, sin archivo. No se ofrece otra carpeta.
- **Límite diario del servicio agotado a mitad:** se rechaza todo; no se guarda una traducción parcial.
- **Ajuste desactivado con una traducción en curso:** la traducción en curso se cancela.
- **Pista no disponible (archivo movido):** la acción avisa y no hace nada.
- **Nombre del archivo con caracteres especiales:** el hermano se nombra a partir de la ruta del audio con
  `Path`, sin componer rutas con texto del usuario ni de la respuesta.

## Decisiones abiertas (se cierran en M0, con el autor)

1. **Qué servicio.** Tiene que ser gratuito, sin clave y con condiciones de uso que permitan este uso. Los
   candidatos se evalúan leyendo su documentación actual, no de memoria: límites por petición y por día,
   detección automática del idioma de origen, formato de la respuesta y condiciones. Se trae una recomendación
   con las fuentes.
2. **Idioma de origen.** Automático si el servicio lo admite; si no, hace falta un tercer ajuste o preguntar.
3. **Derechos de autor.** El texto que se envía suele ser una letra con derechos: el aviso de Configuración lo
   dice con claridad. Se revisa su redacción con el autor.
4. **Aviso la primera vez.** Si basta el texto de Configuración (criterio 2) o hace falta confirmar en el primer
   uso.

## Resultado de M0 (15 de noviembre de 2026): qué servicio

Leída la documentación publicada de cada servicio ese día. **No se llamó a ninguna API**: lo que la
documentación no dice queda marcado como «por comprobar».

| | MyMemory (`api.mymemory.translated.net/get`) | LibreTranslate, instancia oficial | LibreTranslate, instancia propia |
| --- | --- | --- | --- |
| Clave | No hace falta | Obligatoria y de pago (desde 29 USD al mes; sin plan gratuito) | No hace falta |
| Límite diario | 5 000 caracteres al día sin identificarse; 50 000 dando un correo de contacto | Sin límite diario | El que ponga quien la aloja |
| Por petición | `q` de 500 bytes como máximo | 2 000 caracteres | Configurable |
| Idioma de origen automático | No aparece en la documentación: **por comprobar** | Sí (`source: "auto"`) | Sí |
| Formato de la respuesta | No aparece en la página de la especificación: **por comprobar** | Objeto con `translatedText` | Igual |
| Lo que exige al usuario | Nada | Pagar | Instalar y mantener un servidor |

**Recomendación: MyMemory sin identificarse**, sabiendo lo que cuesta:

- **5 000 caracteres al día** son una o dos letras, según su largo. El tope diario lo impone el servicio y cuenta
  por conexión, no por app: la spec no puede subirlo. No se envía el correo del usuario para llegar a 50 000 (es
  un dato personal y el criterio 4 lo excluye).
- **500 bytes por petición** obligan a enviar la letra **línea a línea** (juntar varias líneas en una petición
  rompería la correspondencia línea a línea del criterio 12). Una letra de 50 líneas distintas son 50
  peticiones: el tope propio por sesión (criterio 13) se fija con esa cuenta.
- Antes de pedir nada, la app calcula los caracteres de la letra y, si pasan de lo que queda del día (lo que la
  propia app lleva enviado en la sesión), avisa y no empieza: una traducción a medias no se guarda.
- Una línea de más de 500 bytes hace que la letra no se pueda traducir (se avisa); no se parte.

**Alternativa que no se recomienda de entrada:** un campo «Servidor de LibreTranslate» en Configuración para quien
tenga una instancia propia. No tiene límites y detecta el idioma, pero casi nadie tiene un servidor; se puede
añadir después sin cambiar el diseño, porque el traductor ya va detrás de `ILyricsTranslator`.

**Decisiones abiertas, tras M0:**

1. **Servicio:** pendiente de que el autor acepte MyMemory con ese límite diario, o prefiera otra vía.
2. **Idioma de origen:** como la detección automática no está documentada, M1 empieza con una comprobación
   manual de una sola petición. Si no existe, hace falta un ajuste «Idioma de las letras» o preguntar al traducir.
3. **Formato de la respuesta y código de «cuota agotada»:** se comprueban en esa misma petición.

Fuentes: `mymemory.translated.net/doc/usagelimits.php`, `mymemory.translated.net/doc/spec.php`,
`docs.libretranslate.com/guides/api_usage/`, `portal.libretranslate.com`.

## Diseño

- `Core/Interfaces/ILyricsTranslator.cs`: `Task<IReadOnlyList<string>> TranslateAsync(IReadOnlyList<string>
  lines, string targetLanguage, CancellationToken ct)`.
- `Core/Services/LyricsTranslation.cs`: `Prepare(LyricBlock)` (líneas únicas y trozos), `Sanitize(string)`,
  `BuildLrc(LyricBlock, traducciones)`.
- `Infrastructure/HttpLyricsTranslator.cs` y un escritor del hermano (temporal único en la misma carpeta y
  `File.Move` sin sobrescribir; el temporal no usa los prefijos `~tags-`/`~conv-` ni extensión de audio).
- `LyricsViewModel`: `TranslateOnlineCommand`, `CanTranslateOnline`; avisa por el mismo `Notified` que usan los
  demás.
- `LyricsLayerMenu` (Core): la opción nueva del menú, con prueba.

## Hitos

- **M0 (spike, sin código de la app):** elegir el servicio y cerrar las decisiones abiertas con el autor.
- **M1:** `LyricsTranslation` y pruebas en Core.
- **M2:** `HttpLyricsTranslator`, escritor del hermano y pruebas en Infrastructure.
- **M3:** ajustes, Configuración y las dos entradas.
- **M4:** documentación (criterio 18).
- **M5:** prueba a mano con una letra sintética (no se suben letras reales al repositorio): traducir, sin
  conexión, con el hermano ya existente, cambio de pista a mitad y ajuste desactivado (comprobando que no hay
  tráfico).
