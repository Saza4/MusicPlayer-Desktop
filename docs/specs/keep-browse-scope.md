# Spec: conservar el álbum o artista abierto tras guardar etiquetas

Estado: implementada el 19 de noviembre de 2026, **a falta de la prueba a mano del autor** (lista en «Resultado»).
Compila con 0 errores y la advertencia CS8602 de siempre, y pasan las 1061 pruebas (850 en Core, 211 en
Infrastructure), 10 de ellas nuevas (`BrowseScopeTests`).

## Context

Era el punto 1 de `docs/pendientes.md` y lo único que se sabía de la antigua spec de mejoras de uso
(`ui-responsiveness.md`, «Out of scope»). `MainViewModel.OnTagsSaved` (al guardar en el editor de etiquetas y al
aplicar «Limpiar etiquetas») ponía a `null` `_artistScope` y `_albumScope`, así que la vista volvía a la lista
general aunque el álbum siguiera existiendo.

## FEATURE: Tras guardar, la vista sigue donde estaba

### ACCEPTANCE CRITERIA

1. Si el álbum y el artista abiertos siguen existiendo tras guardar, siguen abiertos.
2. Si el álbum abierto ya no existe (se renombró, o cambió su artista del álbum), se abre el álbum de la primera
   pista que se veía y sigue en la biblioteca. Si había un artista abierto, pasa a ser el de ese álbum.
3. Si el artista abierto ya no existe, se sigue a esa misma pista.
4. Si no queda ninguna pista de las que se veían, se cierra lo que ya no existe: el artista se queda si sigue
   existiendo.
5. El grupo del álbum se rehace con la biblioteca nueva (título y artista actualizados en la cabecera); no se
   reutiliza el viejo.
6. La decisión es una función pura en Core (`BrowseScope.Resolve`) con pruebas.

### OUT OF SCOPE

- Conservar la posición de desplazamiento de la lista.
- Las playlists: no tienen álbum ni artista abierto.

### EDGE CASES

- **Se mueve una sola pista a otro álbum y el original conserva otras:** se sigue en el álbum original, sin la
  pista movida.
- **«Limpiar etiquetas» renombra todo el álbum:** se sigue a la primera pista que se veía.
- **Mayúsculas y espacios:** el artista se compara como en el filtro (`Trim` y sin distinguir mayúsculas).

## Resultado

- `src/MusicPlayer.Core/Services/BrowseScope.cs` y `tests/MusicPlayer.Core.Tests/BrowseScopeTests.cs` (10 pruebas).
- `src/MusicPlayer.Desktop/ViewModels/MainViewModel.cs`: `OnTagsSaved`.
- Prueba a mano pendiente (Windows): dentro de un álbum, cambiar el título de una pista; cambiar el nombre del álbum
  de todas sus pistas con el editor o con «Limpiar etiquetas»; dentro de un artista, cambiar el artista de su única
  pista. En los tres casos la vista sigue en ese álbum o artista.
