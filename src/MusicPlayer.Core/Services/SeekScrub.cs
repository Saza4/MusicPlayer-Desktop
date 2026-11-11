namespace MusicPlayer.Core.Services;

/// <summary>
/// Arrastre de la barra de progreso (docs/specs/ui-responsiveness.md, criterios 4 y 5): mientras el puntero está
/// pulsado no se salta en cada valor intermedio ni el temporizador mueve el pulgar; al soltar se salta una vez.
/// </summary>
public sealed class SeekScrub
{
    private bool _active;
    private bool _abandoned;
    private TimeSpan? _pending;

    /// <summary>False durante el arrastre: el temporizador no debe escribir la posición real encima del pulgar.</summary>
    public bool TimerMayWrite => !_active;

    /// <summary>El puntero se pulsó sobre la barra.</summary>
    public void Begin()
    {
        _active = true;
        _abandoned = false;
        _pending = null;
    }

    /// <summary>
    /// El usuario cambió la posición. Devuelve a dónde saltar ahora mismo (un atajo de teclado, un clic en la isla)
    /// o null si el salto queda pendiente hasta soltar.
    /// </summary>
    public TimeSpan? Change(TimeSpan position)
    {
        if (!_active) return position;
        if (!_abandoned) _pending = position;
        return null;
    }

    /// <summary>El puntero se soltó (o se perdió su captura). Devuelve el único salto del arrastre, si lo hay.</summary>
    public TimeSpan? End()
    {
        var target = _abandoned ? null : _pending;
        _active = false;
        _abandoned = false;
        _pending = null;
        return target;
    }

    /// <summary>
    /// La pista cambió o terminó a mitad del arrastre: lo que quede de él no salta en la pista nueva. El temporizador
    /// sigue sin escribir hasta que se suelte, para no pelear con el pulgar.
    /// </summary>
    public void Abandon()
    {
        if (!_active) return;
        _abandoned = true;
        _pending = null;
    }
}
