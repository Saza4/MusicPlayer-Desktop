using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Threading;
using MusicPlayer.Core.Services;
using MusicPlayer.Desktop.Services;

namespace MusicPlayer.Desktop.Views;

/// <summary>
/// Ventana de la isla. Sabe dibujarse, colocarse, aparecer o desaparecer con un fundido y expandirse con el
/// hover; cuándo se ve lo decide <see cref="IslandController"/> y los pasos del hover, <see cref="IslandHover"/>.
/// </summary>
public partial class IslandWindow : Window
{
    private readonly IslandHover _hover = new();
    private readonly DispatcherTimer _collapseDelay;  // espera tras salir el cursor
    private readonly DispatcherTimer _collapseEnd;    // fin de la animación de colapso: entonces se encoge la zona de clics

    // Las transiciones se conservan para cambiar duración y curva según se expanda o se colapse.
    private readonly DoubleTransition _width = new() { Property = WidthProperty };
    private readonly DoubleTransition _height = new() { Property = HeightProperty };
    private readonly CornerRadiusTransition _radius = new() { Property = Border.CornerRadiusProperty };
    private readonly DoubleTransition _shadowFade = new() { Property = OpacityProperty };
    private readonly DoubleTransition _collapsedFade = new() { Property = OpacityProperty };
    private readonly DoubleTransition _expandedFade = new() { Property = OpacityProperty };

    private bool _hitAreaExpanded; // si la zona de clics cubre la ventana entera o solo la pastilla colapsada
    private int _fadeVersion; // invalida un ocultado pendiente si la isla vuelve a mostrarse

    public IslandWindow()
    {
        InitializeComponent();
        // Temporizadores de un solo disparo: se paran al saltar (DispatcherTimer se repite si no).
        _collapseDelay = new DispatcherTimer(IslandMetrics.CollapseDelay, DispatcherPriority.Normal, (_, _) =>
        {
            _collapseDelay!.Stop();
            Run(_hover.CollapseDelayElapsed(IsPointerOverPill()));
        });
        _collapseEnd = new DispatcherTimer(IslandMetrics.CollapseDuration, DispatcherPriority.Normal, (_, _) =>
        {
            _collapseEnd!.Stop();
            Run(_hover.CollapseFinished(IsPointerOverPill()));
        });
        _collapseDelay.Stop();
        _collapseEnd.Stop();

        ApplyMetrics();
        Pill.PointerEntered += (_, _) => Run(_hover.PointerEntered());
        Pill.PointerExited += (_, _) => Run(_hover.PointerExited());
    }

    private void ApplyMetrics()
    {
        SetPillCollapsed();
        Collapsed.Opacity = 1;
        Expanded.Opacity = 0;

        Collapsed.Width = IslandMetrics.CollapsedWidth;
        Collapsed.Height = IslandMetrics.CollapsedHeight;
        Expanded.Width = IslandMetrics.ExpandedWidth;
        Expanded.Height = IslandMetrics.ExpandedHeight;

        // La miniatura queda centrada en vertical con el mismo hueco a la izquierda.
        var inset = (IslandMetrics.CollapsedHeight - IslandMetrics.CollapsedCoverSize) / 2;
        Collapsed.Margin = new Thickness(inset, 0, IslandMetrics.CollapsedHeight / 2, 0);
        Collapsed.Width -= inset + IslandMetrics.CollapsedHeight / 2;
        foreach (var part in new Control[] { CollapsedCover, CollapsedPlaceholder, CollapsedArt })
        {
            part.Width = IslandMetrics.CollapsedCoverSize;
            part.Height = IslandMetrics.CollapsedCoverSize;
        }
        var coverRadius = new CornerRadius(IslandMetrics.CollapsedCoverCornerRadius);
        CollapsedPlaceholder.CornerRadius = coverRadius;
        CollapsedArt.CornerRadius = coverRadius;

        Width = IslandMetrics.WindowSize.Width;
        Height = IslandMetrics.WindowSize.Height;

        // La sombra sigue el tamaño animado de la pastilla y solo cambia de opacidad.
        PillShadow.BoxShadow = IslandMetrics.ExpandedShadow;
        PillShadow.Bind(WidthProperty, Pill.GetObservable(WidthProperty));
        PillShadow.Bind(HeightProperty, Pill.GetObservable(HeightProperty));
        PillShadow.Bind(Border.CornerRadiusProperty, Pill.GetObservable(Border.CornerRadiusProperty));

        Pill.Transitions = new Transitions { _width, _height, _radius };
        PillShadow.Transitions = new Transitions { _shadowFade };
        Collapsed.Transitions = new Transitions { _collapsedFade };
        Expanded.Transitions = new Transitions { _expandedFade };
        Root.Transitions = new Transitions
        {
            new DoubleTransition { Property = OpacityProperty, Duration = IslandMetrics.FadeDuration }
        };
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        NonActivatingWindow.Apply(this);
        Screens.Changed += (_, _) => PlaceOnPrimaryScreen();
        ScalingChanged += (_, _) => PlaceOnPrimaryScreen();
        PlaceOnPrimaryScreen();
    }

    // ---- Hover ----

    /// <summary>Ejecuta los pasos que pide <see cref="IslandHover"/>, siempre en este orden.</summary>
    private void Run(IslandHoverActions actions, bool animate = true)
    {
        if (actions.HasFlag(IslandHoverActions.CancelCollapseDelay)) _collapseDelay.Stop();
        if (actions.HasFlag(IslandHoverActions.GrowHitArea)) SetHitArea(expanded: true);
        if (actions.HasFlag(IslandHoverActions.ExpandPill)) ExpandPill();
        if (actions.HasFlag(IslandHoverActions.StartCollapseDelay)) _collapseDelay.Start();
        if (actions.HasFlag(IslandHoverActions.CollapsePill)) CollapsePill(animate);
        if (actions.HasFlag(IslandHoverActions.ShrinkHitArea))
        {
            _collapseEnd.Stop();
            SetHitArea(expanded: false);
        }
    }

    private void ExpandPill()
    {
        _collapseEnd.Stop(); // volver a entrar mientras se colapsa cancela el encogimiento de la zona de clics
        Retime(IslandMetrics.ExpandDuration, IslandMetrics.ExpandEasing);
        _collapsedFade.Delay = TimeSpan.Zero;
        _expandedFade.Delay = IslandMetrics.ExpandedContentDelay;

        Pill.Width = IslandMetrics.ExpandedWidth;
        Pill.Height = IslandMetrics.ExpandedHeight;
        Pill.CornerRadius = new CornerRadius(IslandMetrics.ExpandedCornerRadius);
        PillClip.CornerRadius = Pill.CornerRadius;
        PillShadow.Opacity = 1;
        Collapsed.Opacity = 0;
        Expanded.Opacity = 1;
    }

    private void CollapsePill(bool animate)
    {
        Retime(IslandMetrics.CollapseDuration, IslandMetrics.CollapseEasing);
        _expandedFade.Delay = TimeSpan.Zero;
        _collapsedFade.Delay = IslandMetrics.CollapseDuration - IslandMetrics.ContentFadeDuration;

        if (!animate)
        {
            // Sin transiciones: la isla está oculta y la próxima vez debe aparecer ya colapsada.
            var pill = Pill.Transitions;
            var shadow = PillShadow.Transitions;
            var collapsed = Collapsed.Transitions;
            var expanded = Expanded.Transitions;
            Pill.Transitions = PillShadow.Transitions = Collapsed.Transitions = Expanded.Transitions = null;
            SetPillCollapsed();
            Collapsed.Opacity = 1;
            Expanded.Opacity = 0;
            Pill.Transitions = pill;
            PillShadow.Transitions = shadow;
            Collapsed.Transitions = collapsed;
            Expanded.Transitions = expanded;
            return;
        }

        SetPillCollapsed();
        Collapsed.Opacity = 1;
        Expanded.Opacity = 0;
        _collapseEnd.Start();
    }

    private void SetPillCollapsed()
    {
        Pill.Width = IslandMetrics.CollapsedWidth;
        Pill.Height = IslandMetrics.CollapsedHeight;
        Pill.CornerRadius = new CornerRadius(IslandMetrics.CollapsedCornerRadius);
        PillClip.CornerRadius = Pill.CornerRadius;
        PillShadow.Opacity = 0;
    }

    private void Retime(TimeSpan duration, Avalonia.Animation.Easings.Easing easing)
    {
        foreach (var t in new TransitionBase[] { _width, _height, _radius, _shadowFade })
        {
            t.Duration = duration;
            t.Easing = easing;
        }
        _collapsedFade.Duration = IslandMetrics.ContentFadeDuration;
        _expandedFade.Duration = IslandMetrics.ContentFadeDuration;
    }

    /// <summary>
    /// Si el cursor real está sobre la pastilla (con su tamaño de este momento). No se usa el hover de
    /// Avalonia porque, con el cursor quieto mientras la pastilla cambia de tamaño, puede quedar desfasado.
    /// </summary>
    private bool IsPointerOverPill()
    {
        if (WindowNative.CursorPosition() is not { } cursor) return Pill.IsPointerOver;
        var topLeft = Pill.PointToScreen(new Point(0, 0));
        var bottomRight = Pill.PointToScreen(new Point(Pill.Bounds.Width, Pill.Bounds.Height));
        return cursor.X >= topLeft.X && cursor.X < bottomRight.X
            && cursor.Y >= topLeft.Y && cursor.Y < bottomRight.Y;
    }

    // ---- Posición y tamaño ----

    private void SetHitArea(bool expanded)
    {
        _hitAreaExpanded = expanded;
        ApplyHitArea(RenderScaling);
    }

    /// <summary>
    /// La ventana no cambia de tamaño ni de sitio (si lo hiciera, la pastilla saltaría un fotograma): lo que
    /// cambia es la zona que recibe clics. En reposo es la pastilla colapsada con sus esquinas redondeadas, más
    /// <see cref="IslandMetrics.CollapsedHitMargin"/> alrededor: la región corta sin suavizar, y así el corte cae
    /// en zona transparente y no sobre el borde suavizado. Expandida, la ventana entera (pastilla + sombra).
    /// </summary>
    private void ApplyHitArea(double scaling)
    {
        var window = IslandMetrics.WindowSize;
        if (_hitAreaExpanded)
        {
            WindowNative.SetHitRegion(this, new PixelRect(0, 0,
                IslandPlacement.ToPhysical(window.Width, scaling),
                IslandPlacement.ToPhysical(window.Height, scaling)), cornerRadius: 0);
            return;
        }
        // La pastilla va arriba al centro de la ventana. Arriba no hace falta margen: es el borde de la ventana.
        var m = IslandMetrics.CollapsedHitMargin;
        var left = (window.Width - IslandMetrics.CollapsedWidth) / 2 - m;
        WindowNative.SetHitRegion(this, new PixelRect(
                IslandPlacement.ToPhysical(left, scaling), IslandPlacement.ToPhysical(-m, scaling),
                IslandPlacement.ToPhysical(IslandMetrics.CollapsedWidth + 2 * m, scaling),
                IslandPlacement.ToPhysical(IslandMetrics.CollapsedHeight + 2 * m, scaling)),
            IslandPlacement.ToPhysical(IslandMetrics.CollapsedCornerRadius + m, scaling));
    }

    /// <summary>Centrada arriba en el área de trabajo de la pantalla principal (respeta una barra de tareas superior).</summary>
    public void PlaceOnPrimaryScreen()
    {
        if ((Screens.Primary ?? (Screens.All.Count > 0 ? Screens.All[0] : null)) is not { } screen) return;

        var area = screen.WorkingArea;
        var (x, y) = IslandPlacement.TopCenter(area.X, area.Y, area.Width, screen.Scaling, Width, IslandMetrics.TopMargin);
        var target = new PixelPoint(x, y);
        if (Position != target)
            Position = target;
        ApplyHitArea(screen.Scaling);
    }

    // ---- Mostrar y ocultar ----

    public void ShowFaded()
    {
        _fadeVersion++;
        if (!IsVisible)
        {
            Root.Opacity = 0;
            PlaceOnPrimaryScreen(); // antes de Show: aparece ya en su sitio
            Show();
        }
        // En el siguiente ciclo, para que la transición parta de 0 una vez que la ventana existe.
        Dispatcher.UIThread.Post(() => Root.Opacity = 1, DispatcherPriority.Render);
    }

    public async void HideFaded()
    {
        if (!IsVisible) return;
        var version = ++_fadeVersion;
        Root.Opacity = 0;
        await Task.Delay(IslandMetrics.FadeDuration);
        if (version != _fadeVersion) return; // alguien la volvió a mostrar mientras se desvanecía
        Hide();
        Run(_hover.Reset(), animate: false); // la próxima vez aparece colapsada
    }
}
