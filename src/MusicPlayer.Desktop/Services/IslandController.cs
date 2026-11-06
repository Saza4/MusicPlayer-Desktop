using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using MusicPlayer.Core.Services;
using MusicPlayer.Desktop.ViewModels;
using MusicPlayer.Desktop.Views;

namespace MusicPlayer.Desktop.Services;

/// <summary>
/// Decide cuándo se ve la isla (regla en <see cref="IslandVisibility"/>) y la crea la primera vez que hace falta.
/// Cada cambio relevante reinicia una espera corta (<see cref="IslandMetrics.VisibilityDelay"/>): solo se actúa
/// cuando el estado se estabiliza, así un Alt+Tab rápido de ida y vuelta, o el hueco entre cerrar un selector y
/// reactivarse la ventana principal, no hacen parpadear la isla. Solo funciona en Windows.
/// </summary>
public sealed class IslandController : IDisposable
{
    private readonly MainViewModel _viewModel;
    private readonly ModalTracker _modals;
    private readonly DispatcherTimer _settle;
    private Window? _main;
    private IslandWindow? _island;

#if DEBUG
    private static readonly bool ForceVisible =
        Environment.GetEnvironmentVariable("MUSICPLAYER_ISLAND_FORCE") == "1";
#endif

    public IslandController(MainViewModel viewModel, ModalTracker modals)
    {
        _viewModel = viewModel;
        _modals = modals;
        _settle = new DispatcherTimer(IslandMetrics.VisibilityDelay, DispatcherPriority.Normal, (_, _) => Apply());
    }

    public void Attach(Window main)
    {
        if (!OperatingSystem.IsWindows() || _main is not null) return;

        _main = main;
        _viewModel.PropertyChanged += OnViewModelChanged;
        _modals.Changed += OnModalsChanged;
        main.PropertyChanged += OnMainWindowChanged;
        main.Closed += OnMainWindowClosed;
        Schedule();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.CurrentTrack)
            or nameof(MainViewModel.ShowIsland)
            or nameof(MainViewModel.IsImmersive))
            Schedule();
    }

    private void OnModalsChanged(object? sender, EventArgs e) => Schedule();

    private void OnMainWindowChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WindowBase.IsActiveProperty || e.Property == Window.WindowStateProperty)
            Schedule();
    }

    private void OnMainWindowClosed(object? sender, EventArgs e) => Dispose();

    private void Schedule()
    {
        _settle.Stop();
        _settle.Start();
    }

    private void Apply()
    {
        _settle.Stop();
        if (_main is null) return;

        var mainActive = _main.IsActive && _main.WindowState != WindowState.Minimized;
#if DEBUG
        // Solo en Debug (no se compila en Release): MUSICPLAYER_ISLAND_FORCE=1 muestra la isla aunque la
        // ventana principal esté activa, para probar el hover a mano y tomar capturas. El resto de la regla
        // (pista cargada, «Mostrar isla», inmersivo, selectores) sigue igual.
        if (ForceVisible) mainActive = false;
#endif
        var show = IslandVisibility.ShouldShow(
            hasTrack: _viewModel.CurrentTrack is not null,
            showIsland: _viewModel.ShowIsland,
            isImmersive: _viewModel.IsImmersive,
            isMainWindowActive: mainActive,
            isModalOpen: _modals.IsOpen);

        if (show)
            (_island ??= CreateIsland()).ShowFaded();
        else
            _island?.HideFaded();
    }

    private IslandWindow CreateIsland()
    {
        var island = new IslandWindow { DataContext = _viewModel };
        island.OpenMainRequested += (_, _) => BringMainToFront();
        return island;
    }

    /// <summary>
    /// Clic en la carátula o el título de la isla: restaura la ventana principal si está minimizada (a su
    /// estado anterior, maximizada incluida) y la activa. Si Windows no le da el primer plano con Activate(),
    /// lo pide directamente (plan B). Al activarse, la regla de visibilidad oculta la isla.
    /// </summary>
    private void BringMainToFront()
    {
        if (_main is null) return;
        if (_main.WindowState == WindowState.Minimized && !WindowNative.Restore(_main))
            _main.WindowState = WindowState.Normal;
        _main.Show();
        _main.Activate();
        if (!WindowNative.IsForeground(_main))
            WindowNative.ForceForeground(_main);
    }

    public void Dispose()
    {
        _settle.Stop();
        _viewModel.PropertyChanged -= OnViewModelChanged;
        _modals.Changed -= OnModalsChanged;
        if (_main is not null)
        {
            _main.PropertyChanged -= OnMainWindowChanged;
            _main.Closed -= OnMainWindowClosed;
            _main = null;
        }
        _island?.Close();
        _island = null;
    }
}
