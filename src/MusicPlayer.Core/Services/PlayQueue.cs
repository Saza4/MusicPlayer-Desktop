using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Services;

/// <summary>Cola de reproducción con orden normal o barajado y modos de repetición. Sin UI ni audio.</summary>
public sealed class PlayQueue<T> where T : class
{
    private readonly Random _random;
    private T[] _items = Array.Empty<T>();
    private int[] _order = Array.Empty<int>(); // índices de _items en el orden en que suenan
    private int _position = -1;                // posición dentro de _order
    private int _pendingNext;                  // pistas de "Reproducir siguiente" que aún no han sonado
    private bool _shuffle;

    public PlayQueue(Random? random = null) => _random = random ?? Random.Shared;

    public RepeatMode Repeat { get; set; }

    public bool Shuffle
    {
        get => _shuffle;
        set
        {
            if (_shuffle == value) return;
            _shuffle = value;
            Reorder();
        }
    }

    public int Count => _items.Length;

    /// <summary>La pista en curso. Si se quitó, es la anterior a ella: así "Siguiente" continúa donde iba.</summary>
    public T? Current => _position >= 0 ? _items[_order[_position]] : null;

    /// <summary>Reemplaza la cola. Con Aleatorio, <paramref name="startIndex"/> suena primero.</summary>
    public void Start(IEnumerable<T> items, int startIndex)
    {
        var list = items.ToArray();
        ArgumentOutOfRangeException.ThrowIfNegative(startIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(startIndex, list.Length);

        _items = list;
        _order = _shuffle ? Shuffled(startIndex) : Sequential();
        _position = Array.IndexOf(_order, startIndex);
        _pendingNext = 0;
    }

    /// <summary>Hace sonar esta pista justo después de la actual (detrás de las que ya se pidieron "siguiente").</summary>
    public void PlayNext(T item)
    {
        var at = Math.Min(_position + 1 + _pendingNext, _order.Length);
        Insert(item, at);
        _pendingNext++;
    }

    /// <summary>Añade esta pista al final de lo que queda por sonar.</summary>
    public void Enqueue(T item) => Insert(item, _order.Length);

    /// <summary>Siguiente por acción del usuario: ignora "Repetir una". Null al llegar al final.</summary>
    public T? MoveNext()
    {
        if (_items.Length == 0) return null;

        if (_position + 1 < _order.Length)
        {
            _position++;
            _pendingNext = Math.Max(0, _pendingNext - 1);
            return Current;
        }

        if (Repeat != RepeatMode.All) return null;

        // Fin de la cola con "repetir todas": empieza otra vuelta.
        var last = _position >= 0 ? _order[_position] : -1;
        _order = _shuffle ? Shuffled(first: -1, avoidFirst: last) : Sequential();
        _position = 0;
        _pendingNext = 0;
        return Current;
    }

    /// <summary>Qué suena cuando la pista termina sola: la misma con "Repetir una", si no la siguiente.</summary>
    public T? MoveNextAfterEnd() =>
        Repeat == RepeatMode.One && Current is { } same ? same : MoveNext();

    /// <summary>
    /// Qué sonaría si la pista terminara ahora, sin avanzar la cola. Null si no hay siguiente
    /// o no se puede saber de antemano (la vuelta de "Repetir todas" con Aleatorio se baraja al empezar).
    /// </summary>
    public T? PeekNextAfterEnd()
    {
        if (_position < 0) return null;
        if (Repeat == RepeatMode.One) return Current;
        if (_position + 1 < _order.Length) return _items[_order[_position + 1]];

        // Fin de la cola: con "Repetir todas" sin Aleatorio la vuelta empieza por la primera pista.
        return Repeat == RepeatMode.All && !_shuffle ? _items[0] : null;
    }

    /// <summary>Pista anterior, o null si ya estamos en la primera.</summary>
    public T? MovePrevious()
    {
        if (_position <= 0) return null;

        _position--;
        _pendingNext = 0;
        return Current;
    }

    /// <summary>Quita de la cola las pistas que cumplan la condición, conservando el orden y la posición.</summary>
    public void RemoveWhere(Predicate<T> shouldRemove)
    {
        var map = new int[_items.Length]; // índice viejo -> índice nuevo (-1 si se quita)
        var kept = new List<T>(_items.Length);
        for (int i = 0; i < _items.Length; i++)
        {
            if (shouldRemove(_items[i])) map[i] = -1;
            else { map[i] = kept.Count; kept.Add(_items[i]); }
        }

        var order = new List<int>(kept.Count);
        var position = -1;
        for (int p = 0; p < _order.Length; p++)
        {
            if (map[_order[p]] >= 0) order.Add(map[_order[p]]);
            if (p == _position) position = order.Count - 1;
        }

        _items = kept.ToArray();
        _order = order.ToArray();
        _position = position;
        _pendingNext = 0;
    }

    private void Insert(T item, int orderPosition)
    {
        _items = _items.Append(item).ToArray();

        var order = new List<int>(_order);
        order.Insert(orderPosition, _items.Length - 1);
        _order = order.ToArray();
    }

    // Al activar o desactivar Aleatorio, la pista en curso no cambia.
    private void Reorder()
    {
        if (_items.Length == 0) return;

        var current = _position >= 0 ? _order[_position] : -1;
        _order = _shuffle ? Shuffled(current) : Sequential();
        _position = current >= 0 ? Array.IndexOf(_order, current) : -1;
        _pendingNext = 0;
    }

    private int[] Sequential() => Enumerable.Range(0, _items.Length).ToArray();

    // Fisher-Yates. "first" (si existe) abre la cola; "avoidFirst" evita repetir la última pista de la vuelta anterior.
    private int[] Shuffled(int first, int avoidFirst = -1)
    {
        var order = Sequential();
        for (int i = order.Length - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }

        if (first >= 0)
        {
            var at = Array.IndexOf(order, first);
            (order[0], order[at]) = (order[at], order[0]);
        }
        else if (order.Length > 1 && order[0] == avoidFirst)
        {
            (order[0], order[^1]) = (order[^1], order[0]);
        }

        return order;
    }
}