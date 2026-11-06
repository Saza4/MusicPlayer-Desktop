# Especificación: release v1.3.0

Estado: **preparada** (5 de noviembre de 2026). La release está como borrador en GitHub; la publica el autor.

## Contexto

La última release publicada es v1.2.0 (3 de noviembre de 2026). Desde entonces `main` acumula tres funciones que los usuarios aún no tienen: los **temas de la interfaz**, los **atajos de salto, volumen y silencio** y la **isla flotante** (solo Windows). Esta release las publica con notas que digan qué hace el código y qué se probó.

Las notas anteriores prometían más de lo que hacía el código (reporte, sección 6): una «lectura avanzada» de `.lrc` que ya existía, una «pausa mejorada» sin cambio real y funciones omitidas. Esta spec existe para no repetirlo.

## Decisiones (del autor)

1. **Versión v1.3.0.** Funciones nuevas y nada incompatible: un `settings.json` de v1.2.0 sigue funcionando. No hay motivo para un salto mayor ni basta un parche.
2. **El zip incluye las DLL de BASS**, como el de v1.2.0 (`bass.dll`, `bass_aac.dll`, `bassflac.dll`, `bassmix.dll`). El usuario descomprime y ejecuta; solo necesita el runtime de .NET 8. BASS es gratuito solo para uso no comercial: el autor debe confirmar en un4seen.com que puede redistribuirlo antes de publicar.
3. **Borrador en GitHub.** Se prepara todo y la release se deja sin publicar con el zip adjunto. Con `--draft` el tag no existe hasta que el autor publica.
4. **Sin `<Version>` en el `.csproj`.** La versión vive en el tag, el título de la release y el nombre del zip, como hasta ahora.

## Criterios de aceptación

1. `main` contiene todo lo de v1.3.0 y compila: `dotnet build -c Release` solo con la CS8602 conocida y 403 pruebas en verde (345 en Core y 58 en Infrastructure).
2. `docs/notas-de-version/v1.3.0.md` está en español y no afirma nada que el código no haga. Cada función cita sus nombres reales: el botón «Tema», `Ctrl+↑` y `Ctrl+↓`, «Mostrar isla flotante» y la carpeta `%APPDATA%\MusicPlayer\themes`.
3. Las notas incluyen novedades, **«Al actualizar»**, requisitos, **qué se probó y qué no**, y la descarga con su SHA-256.
4. El zip `MusicPlayer-v1.3.0-win-x64.zip` se construye con `dotnet publish -c Release -r win-x64 --self-contained false`, con la misma estructura que el de v1.2.0 (carpeta raíz `win-x64/`). Lleva las cuatro DLL de BASS y **no** contiene el código de depuración de la isla (`MUSICPLAYER_ISLAND_FORCE`).
5. La release de GitHub es un **borrador** titulado «MusicPlayer v1.3.0», con el zip adjunto y apuntando a un commit de `main`.
6. Docs al día: README (sección «Descargar» y la contradicción de BASS), reporte (cabecera, tablas, secciones 5 y 6) y la spec de la isla.

## Fuera de alcance

Firmar el ejecutable, un instalador, la versión dentro del `.csproj`, un workflow de release automático, un build para Linux y cualquier cambio de código de la app.

## Casos límite

- **Zip sin las DLL de BASS:** la app no inicia. Se comprueba el contenido del zip y se arranca el exe publicado.
- **`settings.json` de v1.2.0** (sin `ShowIsland` ni `ThemeId`): la app arranca con la isla activada y el tema Oscuro.
- **Linux, o Windows sin .NET 8:** el zip es solo para Windows x64 y necesita el runtime de .NET 8; las notas lo dicen.
- **Promesas no probadas:** el recorrido completo de todos los paneles con el tema Claro y con temas propios no se ha hecho, y Linux no se ha probado. Las notas lo dicen.
- **Aviso al actualizar:** la isla aparece **activada por defecto** en Windows, también para quien ya usaba v1.2.0.

## Compatibilidad verificada

- `AppSettings` añade `ThemeId = "dark"` y `ShowIsland = true` **al final** y con valor por defecto, así que un JSON antiguo se deserializa sin cambios. Un `ThemeId` guardado que ya no existe hace arrancar la app en Oscuro sin sobrescribir el ajuste.
- `library.json` y `playlists.json` siguen en `Version = 1`.
- Dependencias sin cambios: Avalonia 11.2.5 y .NET 8.
- El tema por defecto sigue siendo Oscuro, con los mismos colores que antes de los temas.

## Pasos y resultado

| # | Paso | Resultado |
|---|---|---|
| 1 | Spec y notas (`docs/notas-de-version/v1.3.0.md`) | Hecho |
| 2 | README, reporte y spec de la isla al día | Hecho |
| 3 | Compilar, probar y publicar el zip desde el código de `main` | Hecho (ver «Resultado») |
| 4 | PR de la release contra `main`, CI en verde y fusión | Este PR |
| 5 | Release en borrador con el zip | Se crea al fusionar este PR; el enlace va en un comentario del PR |
| 6 | **El autor** revisa el borrador, confirma la licencia de BASS y pulsa «Publish release» | Pendiente |

## Resultado

Zip construido con el código de `main` (la rama de la release solo cambia documentación: `git diff main -- src tests` vacío).

| Comprobación | Resultado |
|---|---|
| `dotnet build -c Release` | 0 errores, solo la CS8602 conocida |
| `dotnet test -c Release` | 403 en verde (345 Core + 58 Infrastructure) |
| `dotnet publish -c Release -r win-x64 --self-contained false` | 49 archivos, **el mismo conjunto** que el zip de v1.2.0 (nada falta, nada sobra), con `.pdb` y `runtimeconfig.json` |
| Zip `MusicPlayer-v1.3.0-win-x64.zip` | 11 740 565 bytes, carpeta raíz `win-x64/`, sin rutas con barra invertida |
| SHA-256 | `FE5640786A3E63AC2B70F806FF4CBB6229AC5E7DEF7D10DBEB7EC8D4AD8ABE6C` |
| DLL de BASS en el zip | `bass.dll`, `bass_aac.dll`, `bassflac.dll` y `bassmix.dll` |
| Código de depuración de la isla | `MUSICPLAYER_ISLAND_FORCE` **no** está en el binario publicado |
| Arranque del exe publicado, con un `settings.json` de v1.2.0 (sin `ShowIsland` ni `ThemeId`) | Arranca; botón «Ocultar isla flotante» (isla **activada**); colores del tema Oscuro (`#0F0F0F` y `#000000`); Reproducir pasa a «Pausa» (BASS carga); al minimizar aparece la isla en `L=774 T=8`; cierre limpio, código de salida 0 |
| Datos del usuario | `settings.json` y `library.json` restaurados idénticos (mismo hash); la carpeta de temas intacta |

El zip se construyó una sola vez: es el mismo que se adjunta al borrador y el que se publica, con el SHA-256 de las notas.
