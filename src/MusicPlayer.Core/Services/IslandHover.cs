namespace MusicPlayer.Core.Services;

public enum IslandHoverState
{
    /// <summary>Pastilla colapsada; solo la pastilla recibe clics.</summary>
    Collapsed,
    /// <summary>Zona de clics grande y pastilla expandida (o expandiéndose).</summary>
    Expanded,
    /// <summary>El cursor salió; corre la espera antes de colapsar.</summary>
    PendingCollapse,
    /// <summary>La pastilla se encoge con la zona de clics aún grande; al terminar, se encoge la zona.</summary>
    Collapsing,
}

/// <summary>Lo que la ventana debe hacer, en este orden.</summary>
[Flags]
public enum IslandHoverActions
{
    None = 0,
    CancelCollapseDelay = 1,
    GrowHitArea = 2,
    ExpandPill = 4,
    StartCollapseDelay = 8,
    CollapsePill = 16,
    ShrinkHitArea = 32,
}

/// <summary>
/// Estados del hover de la isla. La zona que recibe clics mide lo justo: en reposo, la pastilla. Al expandir,
/// primero crece la zona y luego anima la pastilla; al colapsar, anima la pastilla y al final se encoge la
/// zona. El hover depende de la pastilla, no de la ventana, y cada paso que podría colapsar comprueba antes
/// dónde está el cursor de verdad: así un <c>PointerExited</c> espurio no colapsa nada, y volver a entrar
/// mientras se colapsa cancela el encogimiento.
/// </summary>
public sealed class IslandHover
{
    public IslandHoverState State { get; private set; } = IslandHoverState.Collapsed;

    public IslandHoverActions PointerEntered()
    {
        var actions = State switch
        {
            IslandHoverState.Collapsed => IslandHoverActions.GrowHitArea | IslandHoverActions.ExpandPill,
            IslandHoverState.PendingCollapse => IslandHoverActions.CancelCollapseDelay,
            // La zona sigue grande: la pastilla vuelve a crecer desde su tamaño actual.
            IslandHoverState.Collapsing => IslandHoverActions.ExpandPill,
            _ => IslandHoverActions.None,
        };
        State = IslandHoverState.Expanded;
        return actions;
    }

    public IslandHoverActions PointerExited()
    {
        if (State != IslandHoverState.Expanded) return IslandHoverActions.None;
        State = IslandHoverState.PendingCollapse;
        return IslandHoverActions.StartCollapseDelay;
    }

    /// <summary>Terminó la espera tras salir. Si el cursor está sobre la pastilla, la salida fue falsa.</summary>
    public IslandHoverActions CollapseDelayElapsed(bool pointerOverPill)
    {
        if (State != IslandHoverState.PendingCollapse) return IslandHoverActions.None;
        if (pointerOverPill)
        {
            State = IslandHoverState.Expanded;
            return IslandHoverActions.None;
        }
        State = IslandHoverState.Collapsing;
        return IslandHoverActions.CollapsePill;
    }

    /// <summary>
    /// Terminó la animación de colapso. Si el cursor volvió sin que llegara un <c>PointerEntered</c>,
    /// se vuelve a expandir en lugar de encoger la zona.
    /// </summary>
    public IslandHoverActions CollapseFinished(bool pointerOverPill)
    {
        if (State != IslandHoverState.Collapsing) return IslandHoverActions.None;
        if (pointerOverPill)
        {
            State = IslandHoverState.Expanded;
            return IslandHoverActions.ExpandPill;
        }
        State = IslandHoverState.Collapsed;
        return IslandHoverActions.ShrinkHitArea;
    }

    /// <summary>La isla se ocultó: la próxima vez aparece colapsada. La ventana aplica el cambio sin animar.</summary>
    public IslandHoverActions Reset()
    {
        var wasCollapsed = State == IslandHoverState.Collapsed;
        State = IslandHoverState.Collapsed;
        return wasCollapsed
            ? IslandHoverActions.None
            : IslandHoverActions.CancelCollapseDelay | IslandHoverActions.CollapsePill | IslandHoverActions.ShrinkHitArea;
    }
}
