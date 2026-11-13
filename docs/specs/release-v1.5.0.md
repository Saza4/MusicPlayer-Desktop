# Especificación: release v1.5.0

Estado: **preparada** (11 de noviembre de 2026) y **revisada con una auditoría previa** (12 de noviembre, ver al
final). Notas escritas y zip construido y comprobado. El autor dio el visto bueno al borrador el 12 de noviembre:
**falta** crearlo en GitHub cuando el PR de esta documentación esté fusionado, y publicarlo, que lo hace el autor.

## Contexto

La última release publicada es v1.4.0 (8 de noviembre de 2026). Desde entonces `main` acumula:

- La **lectura y el relleno por palabra de los `.lrc` enhanced** (PR #9 y la fusión local `3ae9ff9`; specs
  `lrc-enhanced.md` y `lrc-enhanced-fill.md`).
- Los **arreglos de dos auditorías de solo lectura** (commit `b5dd6c8` y PR #10): `library-data-safety.md`,
  `playback-resilience.md`, `untrusted-input-limits.md`, `library-data-safety-2.md`, `ui-responsiveness.md`,
  `audit-followup.md` y `test-waits-and-docs.md`, más la medida de `lyrics-render-perf.md`.

Son 18 commits y 146 pruebas más. Las notas dicen lo que hace el código y qué se probó y qué no, como las de
v1.3.0 y v1.4.0.

## Decisiones

1. **Versión v1.5.0.** Una función nueva y arreglos, nada incompatible: un `settings.json`, `library.json` o
   `playlists.json` anterior carga igual y no hay ajustes nuevos. Salto menor según SemVer. Release normal
   (**Latest**), no pre-release.
2. **«Qué se probó y qué no» sale de resultados reales.** La pasada manual la hizo el autor el 11 de noviembre de
   2026 y la dio por buena entera, incluidos el arrastre de la barra y el cierre durante una conversión de varias
   pistas. No anotó el resultado paso a paso, y las notas lo dicen así.
3. **Las notas avisan del borrado automático de temporales.** Es el único caso en que la app borra archivos de la
   carpeta de música sin preguntar (D6): va en «Al actualizar», con el patrón exacto.
4. **El zip incluye las DLL de BASS**, como los anteriores (BASS es gratuito solo para uso no comercial; las
   notas lo dicen).
5. **Borrador en GitHub** (`gh release create --draft`): el tag no existe hasta que el autor publica.
6. **Sin `<Version>` en el `.csproj`:** la versión vive en el tag, el título y el nombre del zip.
7. **Integridad:** SHA-256 del zip en las notas; el zip no está firmado y las notas lo dicen.
8. **Los hallazgos de la auditoría previa no retrasan la release** (autor, 12 de noviembre de 2026). Ninguno es
   crítico ni pierde datos: se publican con el zip ya comprobado, las notas los dicen y quedan en
   `docs/pendientes.md` (puntos 5 a 9).
9. **La ruta de compilación dentro de los binarios se deja como en v1.4.0** (autor, 12 de noviembre de 2026) y se
   corrige desde la próxima release.

## Criterios de aceptación

1. `main` (`91c4b34`) compila con `dotnet build -c Release` (solo la CS8602 conocida) y pasa 959 pruebas (755
   Core + 204 Infrastructure). El CI del PR #10 pasó en Windows y Ubuntu.
2. `docs/notas-de-version/v1.5.0.md` está en español, no afirma nada que el código no haga e incluye novedades,
   arreglos, «Al actualizar», requisitos, qué se probó y qué no, cómo verificar la descarga (SHA-256) y los
   contribuyentes. Los textos de la interfaz que cita («Error inesperado: …», «temporales antiguos eliminados»,
   «Se borró el archivo, pero no se pudo guardar la biblioteca: …») son los del código.
3. El zip `MusicPlayer-v1.5.0-win-x64.zip` se construye con
   `dotnet publish -c Release -r win-x64 --self-contained false`, con la misma estructura que el de v1.4.0 (49
   archivos, carpeta raíz `win-x64/`, rutas con `/`), con las cuatro DLL de BASS y sin el código de depuración
   (`MUSICPLAYER_ISLAND_FORCE`, `MUSICPLAYER_LYRICS_*`, `MUSICPLAYER_RENDER_OVERLAY`).
4. La release es un **borrador** titulado «MusicPlayer v1.5.0», con ese zip adjunto y apuntando a un commit de
   `main` que solo añade documentación sobre `91c4b34`.
5. Docs al día: reporte (cabecera, tabla 4.1, sección 4.3.8, sección 5 con las notas, 6 y 10).

## Fuera de alcance

Firmar el ejecutable, un instalador, la versión en el `.csproj`, un workflow de release automático, un build para
Linux y cualquier cambio de código de la app. Lo que falta sin spec está en `docs/pendientes.md`.

## Casos límite

- **El zip se reconstruye:** su SHA-256 cambia. El de las notas vale solo para el zip construido una vez; si se
  rehace, hay que cambiar el hash en las notas y en el reporte antes de crear el borrador.
- **Zip creado con Windows PowerShell 5.1:** `Compress-Archive` escribe rutas con `\`. Se usa PowerShell 7.
- **Zip sin las DLL de BASS:** la app no inicia. Se comprueba el contenido y se arranca el exe publicado.
- **Usuario con música llamada `~conv-<32 hex>.mp3`:** la app la borraría al escanear tras 24 horas. Las notas lo
  avisan.
- **Carátula de más de 10 MB o letra de más de 1 MB que antes se veía:** deja de verse. Las notas lo avisan.
- **Promesas no probadas:** relleno por palabra con texto de derecha a izquierda, pantalla táctil y Linux. Las
  notas lo dicen. También el congelamiento con unidades de red y R6, que siguen abiertos.
- **`playlists.json` ilegible al arrancar:** el aviso se pisa con el resumen de la biblioteca y solo se ve al
  intentar un cambio. Las notas lo dicen así y no prometen el aviso al arrancar.
- **Quien abre «Editar etiquetas» con la pista sonando lee «se detendrá»:** el texto es viejo; la pista se
  reanuda. Las notas lo avisan.
- **En pausa, «Anterior» o pulsar una línea de la letra:** el salto se hace, pero la barra no se mueve hasta
  reanudar. Las notas lo dan como fallo conocido.
- **Nombres de archivo o marcas de `.lrc` con dígitos que no son `0`-`9`:** «Limpiar etiquetas» falla para la
  lista visible y esa letra no se muestra. Las notas lo dan como fallo conocido.

## Resultado

Zip construido con el código de `main` (`91c4b34`) el 11 de noviembre de 2026.

| Comprobación | Resultado |
|---|---|
| `dotnet build -c Release` | 0 errores, solo la CS8602 conocida |
| `dotnet test -c Release` | 959 en verde (755 Core + 204 Infrastructure) |
| `dotnet publish -c Release -r win-x64 --self-contained false` | 49 archivos, **el mismo conjunto** que el zip de v1.4.0 (comparado con el zip publicado) |
| Zip `MusicPlayer-v1.5.0-win-x64.zip` | 11 808 198 bytes, carpeta raíz `win-x64/`, sin rutas con barra invertida |
| SHA-256 | `B0E5A7FEB3B282FA9389CADFC218E87FF8F960C400636CC90EE9AD4CCCED34BA` |
| DLL de BASS en el zip | `bass.dll`, `bass_aac.dll`, `bassflac.dll` y `bassmix.dll` |
| Código de depuración | `MUSICPLAYER_ISLAND_FORCE`, `MUSICPLAYER_LYRICS_*` y `MUSICPLAYER_RENDER_OVERLAY` **no** están en `MusicPlayer.Desktop.dll` |
| Arranque del exe publicado | Arranca (ventana «Music Player»), cierre limpio con código de salida 0 |
| Datos del usuario | `settings.json` y `library.json` idénticos antes y después (mismo hash) |

El zip se construyó una sola vez y está fuera del repositorio. Es el que debe adjuntarse al borrador, con el
SHA-256 de las notas.

## Auditoría previa (12 de noviembre de 2026)

A petición del autor, antes del commit se leyó todo `src/` y la documentación (skill `auditor`, solo lectura: sin
compilar ni ejecutar pruebas). Las pruebas se revisaron solo con búsquedas.

| Comprobación | Resultado |
|---|---|
| Hallazgos críticos (pérdida de datos, cierre de la app, reglas de CLAUDE.md sobre escrituras) | Ninguno |
| Fallos de corto plazo en el código | Cuatro, sin arreglar: `docs/pendientes.md`, puntos 5 a 8 |
| Promesas de las notas que el código no cumplía | Dos, corregidas en las notas: el aviso de `playlists.json` y el texto «se detendrá» |
| Documentación desactualizada | Tres frases del reporte (v1.4.0 «borrador», su viñeta «Limitación» y «sin CI»), corregidas |
| Zip | Sigue en su sitio, mismo SHA-256, 49 archivos, cuatro DLL de BASS; su SourceLink apunta a `91c4b34` |
| Rutas locales en el zip | Los DLL y `.pdb` llevan la ruta de compilación del autor, como en v1.4.0 (decisión 9) |
| CI del PR #10 | En verde en Windows y Ubuntu |
| Recuento de 959 pruebas | No verificable por lectura: vale el de `dotnet test` del 11 de noviembre |
