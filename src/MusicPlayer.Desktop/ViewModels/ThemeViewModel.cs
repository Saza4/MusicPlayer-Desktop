using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;
using MusicPlayer.Desktop.Services;

namespace MusicPlayer.Desktop.ViewModels;

/// <summary>Una entrada del selector de tema: «Sistema» o un tema concreto.</summary>
public sealed record ThemeChoice(string Id, string Name);

/// <summary>
/// Selector de tema con vista previa. <see cref="Preview"/> aplica sin guardar, <see cref="Commit"/> aplica y guarda,
/// y al cerrar la lista sin confirmar (<see cref="Close"/>) se vuelve al tema confirmado.
/// </summary>
public partial class ThemeViewModel : ViewModelBase
{
    private readonly IThemeRepository _repository;
    private readonly SettingsService _settings;
    private readonly ThemeService _applier;

    private IReadOnlyList<AppTheme> _themes = BuiltInThemes.All;
    private string _committedId = BuiltInThemes.DarkId;
    private string? _previewId;
    private string? _notice;
    private bool _isOpen;
    private bool _suppressPreview;

    public ThemeViewModel(IThemeRepository repository, SettingsService settings, ThemeService applier)
    {
        _repository = repository;
        _settings = settings;
        _applier = applier;
        RebuildItems(ThemeLoadReport.Empty);
    }

    public ObservableCollection<ThemeChoice> Items { get; } = [];

    /// <summary>Entrada resaltada en la lista. Moverse con las flechas la cambia y previsualiza ese tema.</summary>
    [ObservableProperty]
    private ThemeChoice? _selectedChoice;

    public event Action<string>? Notified;

    /// <summary>La lista está abierta: mientras tanto, los atajos globales de la ventana no deben actuar.</summary>
    public bool IsOpen => _isOpen;

    /// <summary>Flecha arriba/abajo en la lista: mueve el resaltado y, con él, la vista previa.</summary>
    public void MoveSelection(int delta)
    {
        if (Items.Count == 0) return;

        var index = SelectedChoice is null ? -1 : Items.IndexOf(SelectedChoice);
        var next = Math.Clamp(index + delta, 0, Items.Count - 1);
        SelectedChoice = Items[next];
    }

    partial void OnSelectedChoiceChanged(ThemeChoice? value)
    {
        if (!_suppressPreview && _isOpen && value is not null)
            Preview(value.Id);
    }

    /// <summary>
    /// Al arrancar, antes de mostrar la ventana: lee los ajustes y los temas y aplica el elegido.
    /// Cualquier fallo deja el tema Oscuro, que es el aspecto de siempre.
    /// </summary>
    public void Initialize()
    {
        try
        {
            var (saved, report) = Task.Run(async () => (await _settings.LoadAsync(), await _repository.LoadAllAsync()))
                .GetAwaiter().GetResult();

            _committedId = saved.ThemeId;
            var merged = RebuildItems(report);
            var selection = ThemeCatalog.Resolve(_committedId, merged.Themes);
            ApplySelection(selection);

            _notice = BuildNotice(merged, selection.FallbackReason);
            if (_notice is not null) Notified?.Invoke(_notice);
        }
        catch (Exception)
        {
            _applier.Apply(BuiltInThemes.Dark);
        }
    }

    /// <summary>Se abre la lista: se vuelven a leer los temas (así se ve un archivo nuevo sin reiniciar).</summary>
    public async Task OpenAsync()
    {
        _isOpen = true;
        try
        {
            var report = await _repository.LoadAllAsync();
            var merged = RebuildItems(report);
            _notice = BuildNotice(merged, ThemeCatalog.Resolve(_committedId, merged.Themes).FallbackReason);
            if (_notice is not null) Notified?.Invoke(_notice);
        }
        catch (Exception ex)
        {
            Notified?.Invoke($"No se pudieron leer los temas: {ex.Message}");
        }

        SelectCommitted();
    }

    /// <summary>La lista se cerró: si había una vista previa sin confirmar, se restaura el tema confirmado.</summary>
    public void Close()
    {
        _isOpen = false;
        Cancel();
    }

    public void Preview(string id)
    {
        if (string.Equals(_previewId, id, StringComparison.OrdinalIgnoreCase)) return;

        _previewId = id;
        ApplyById(id);
    }

    public void Commit(string id)
    {
        _committedId = id;
        _previewId = null;
        ApplyById(id);
        _settings.Update(s => s with { ThemeId = id });
    }

    /// <summary>Descarta la vista previa y vuelve al tema confirmado (sin tocar el disco).</summary>
    public void Cancel()
    {
        if (_previewId is null) return;

        _previewId = null;
        ApplySelection(ThemeCatalog.Resolve(_committedId, _themes));
        SelectCommitted();
    }

    private void ApplyById(string id) =>
        ApplySelection(ThemeCatalog.Resolve(id, _themes));

    private void ApplySelection(ThemeSelection selection)
    {
        if (selection.FollowsSystem)
            _applier.ApplySystem();
        else
            _applier.Apply(selection.Theme ?? BuiltInThemes.Dark);
    }

    private void SelectCommitted()
    {
        _suppressPreview = true;
        try { SelectedChoice = Items.FirstOrDefault(i => i.Id.Equals(_committedId, StringComparison.OrdinalIgnoreCase)); }
        finally { _suppressPreview = false; }
    }

    private ThemeLoadReport RebuildItems(ThemeLoadReport userReport)
    {
        var merged = ThemeCatalog.Merge(userReport);
        _themes = merged.Themes;

        _suppressPreview = true;
        try
        {
            Items.Clear();
            Items.Add(new ThemeChoice(BuiltInThemes.SystemId, "Sistema"));
            foreach (var theme in merged.Themes)
                Items.Add(new ThemeChoice(theme.Id, theme.Name));
        }
        finally { _suppressPreview = false; }

        return merged;
    }

    private static string? BuildNotice(ThemeLoadReport report, string? fallbackReason)
    {
        if (fallbackReason is not null)
            return fallbackReason;

        if (report.Errors.Count == 0)
            return null;

        var first = report.Errors[0];
        return report.Errors.Count == 1
            ? $"Temas: 1 no se pudo cargar ({first})"
            : $"Temas: {report.Errors.Count} no se pudieron cargar (por ejemplo {first})";
    }
}
