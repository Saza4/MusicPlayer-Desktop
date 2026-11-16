using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;
using MusicPlayer.Infrastructure;

namespace MusicPlayer.Desktop.ViewModels;

/// <summary>
/// Vista «Configuración» (docs/specs/settings-panel.md): una capa dentro de la ventana principal. Cada opción se
/// guarda al cambiarla, sin botón «Guardar». Las opciones que ya tenían dueño (tema, isla) siguen en su ViewModel
/// y la vista las enlaza desde ahí.
/// </summary>
public partial class SettingsViewModel(SettingsService settings) : ObservableObject
{
    private bool _restoring;

    [ObservableProperty] private bool _isOpen;

    /// <summary>Un clic sobre una canción la reproduce (por defecto hace falta doble clic).</summary>
    [ObservableProperty] private bool _playOnSingleClick;

    /// <summary>Las secciones de la isla y del segundo plano solo existen en Windows.</summary>
    public bool IsWindows { get; } = OperatingSystem.IsWindows();

    public string DataFolder => AppPaths.DataDirectory;

    [RelayCommand]
    private void ToggleOpen() => IsOpen = !IsOpen;

    [RelayCommand]
    private void Close() => IsOpen = false;

    partial void OnPlayOnSingleClickChanged(bool value)
    {
        if (!_restoring)
            settings.Update(s => s with { PlayOnSingleClick = value });
    }

    /// <summary>Al arrancar: pone las opciones como estaban guardadas, sin volver a guardarlas.</summary>
    public void Restore(AppSettings saved)
    {
        _restoring = true;
        try
        {
            PlayOnSingleClick = saved.PlayOnSingleClick;
        }
        finally
        {
            _restoring = false;
        }
    }
}
