namespace MusicPlayer.Core.Services;

/// <summary>
/// Contabilidad de las miniaturas que se piden al desplazar la lista (docs/specs/ui-responsiveness.md, criterios 6
/// y 7): qué se está cargando, qué no tiene carátula y qué se invalidó a mitad de una carga. Sin Avalonia ni
/// disco: la carga en sí la hace quien la usa. No es segura entre hilos: se usa solo desde el hilo de la interfaz.
/// </summary>
public sealed class ThumbnailRequests(int missingCapacity = 2000, IEqualityComparer<string>? comparer = null)
{
    private readonly Dictionary<string, int> _loading = new(comparer);
    private readonly LruCache<string, bool> _missing = new(missingCapacity, comparer);
    private int _lastTicket;

    /// <summary>
    /// Pide cargar la miniatura de esa clave. Devuelve el resguardo de la carga que hay que lanzar, o null si ya
    /// hay una en curso o si ya se sabe que no tiene carátula.
    /// </summary>
    public int? Begin(string key)
    {
        if (_loading.ContainsKey(key) || _missing.TryGet(key, out _))
            return null;

        return _loading[key] = ++_lastTicket;
    }

    /// <summary>
    /// La carga con ese resguardo terminó. Devuelve false si el resultado ya no vale (la clave se invalidó mientras
    /// tanto): entonces no debe guardarse ni avisarse a nadie. Un resultado válido sin carátula queda recordado.
    /// </summary>
    public bool Complete(string key, int ticket, bool found)
    {
        if (!_loading.TryGetValue(key, out var current) || current != ticket)
            return false;

        _loading.Remove(key);
        if (!found) _missing.Set(key, true);
        return true;
    }

    /// <summary>La carátula de esa clave cambió: se olvida lo que se sabía y la carga en curso, si hay, deja de valer.</summary>
    public void Invalidate(string key)
    {
        _loading.Remove(key);
        _missing.Remove(key);
    }
}
