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
/// Ventana de la isla. Solo sabe dibujarse, colocarse y aparecer o desaparecer con un fundido;
/// cuándo se ve lo decide <see cref="IslandController"/>.
/// </summary>
public partial class IslandWindow : Window
{
    private int _fadeVersion; // invalida un ocultado pendiente si la isla vuelve a mostrarse

    public IslandWindow()
    {
        InitializeComponent();
        ApplyMetrics();
    }

    private void ApplyMetrics()
    {
        Pill.Width = IslandMetrics.CollapsedWidth;
        Pill.Height = IslandMetrics.CollapsedHeight;
        Pill.CornerRadius = new CornerRadius(IslandMetrics.CollapsedCornerRadius);
        Pill.BoxShadow = IslandMetrics.Shadow;
        Pill.Margin = new Thickness(IslandMetrics.ShadowMargin, 0, IslandMetrics.ShadowMargin, IslandMetrics.ShadowMargin);

        // La miniatura queda centrada en vertical con el mismo hueco a la izquierda.
        var inset = (IslandMetrics.CollapsedHeight - IslandMetrics.CollapsedCoverSize) / 2;
        Collapsed.Margin = new Thickness(inset, 0, IslandMetrics.CollapsedHeight / 2, 0);
        foreach (var part in new Control[] { CollapsedCover, CollapsedPlaceholder, CollapsedArt })
        {
            part.Width = IslandMetrics.CollapsedCoverSize;
            part.Height = IslandMetrics.CollapsedCoverSize;
        }
        var coverRadius = new CornerRadius(IslandMetrics.CollapsedCoverCornerRadius);
        CollapsedPlaceholder.CornerRadius = coverRadius;
        CollapsedArt.CornerRadius = coverRadius;

        var size = IslandMetrics.WindowSizeFor(IslandMetrics.CollapsedWidth, IslandMetrics.CollapsedHeight);
        Width = size.Width;
        Height = size.Height;

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

    /// <summary>Centrada arriba en el área de trabajo de la pantalla principal (respeta una barra de tareas superior).</summary>
    public void PlaceOnPrimaryScreen()
    {
        if ((Screens.Primary ?? (Screens.All.Count > 0 ? Screens.All[0] : null)) is not { } screen) return;

        var area = screen.WorkingArea;
        var (x, y) = IslandPlacement.TopCenter(area.X, area.Y, area.Width, screen.Scaling, Width, IslandMetrics.TopMargin);
        var target = new PixelPoint(x, y);
        if (Position != target)
            Position = target;
    }

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
        if (version == _fadeVersion) // nadie la volvió a mostrar mientras se desvanecía
            Hide();
    }
}
