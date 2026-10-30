using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Desktop.ViewModels;

public partial class TagAutoFixViewModel(TagEditor editor) : ObservableObject
{
    private IReadOnlyList<Track> _scope = Array.Empty<Track>();
    private IReadOnlyList<TagChange> _lastApplied = Array.Empty<TagChange>();

    /// <summary>Antes de escribir: quien reproduce debe soltar los archivos afectados.</summary>
    public event Action<IReadOnlySet<Guid>>? Saving;

    /// <summary>Después de escribir: la biblioteca nueva, ya reordenada.</summary>
    public event Action<LibrarySnapshot>? Applied;

    public event Action<string>? Notified;

    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private string _heading = "";
    [ObservableProperty] private ObservableCollection<TagSuggestionItemViewModel> _items = new();
    [ObservableProperty] private bool _albumFromFolder;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ApplyText))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private int _selectedCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUndo), nameof(UndoText))]
    private int _undoCount;

    public string ApplyText => $"Aplicar a {SelectedCount}";
    public bool HasUndo => UndoCount > 0;
    public string UndoText => $"Etiquetas actualizadas en {Plural.Of(UndoCount, "canción", "canciones")}. Los archivos ya están modificados.";

    /// <summary>Revisa estas pistas y, si hay algo que proponer, abre el panel de vista previa.</summary>
    public void Begin(IReadOnlyList<Track> scope)
    {
        if (scope.Count == 0)
        {
            Notified?.Invoke("No hay canciones disponibles para revisar");
            return;
        }

        _scope = scope;
        _ = AnalyzeAsync(announceWhenEmpty: true);
    }

    partial void OnAlbumFromFolderChanged(bool value)
    {
        if (IsOpen) _ = AnalyzeAsync(announceWhenEmpty: false);
    }

    private async Task AnalyzeAsync(bool announceWhenEmpty)
    {
        IsBusy = true;
        try
        {
            var scope = _scope;
            var options = new TagSuggestOptions(AlbumFromFolder);
            var suggestions = await Task.Run(() => TagSuggester.Suggest(scope, options));

            if (suggestions.Count == 0 && announceWhenEmpty)
            {
                Notified?.Invoke($"Nada que completar en {Plural.Of(scope.Count, "canción", "canciones")}");
                return;
            }

            Items = new ObservableCollection<TagSuggestionItemViewModel>(
                suggestions.Select(s => new TagSuggestionItemViewModel(s, RefreshCount)));
            Heading = $"Autocompletar etiquetas: {Plural.Of(suggestions.Count, "canción", "canciones")} con cambios sugeridos (de {scope.Count} revisadas)";
            RefreshCount();
            IsOpen = true;
        }
        catch (Exception ex)
        {
            Notified?.Invoke($"No se pudo revisar: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    private void RefreshCount() => SelectedCount = Items.Count(i => i.IsSelected);

    [RelayCommand] private void SelectAll() { foreach (var i in Items) i.IsSelected = true; }
    [RelayCommand] private void SelectNone() { foreach (var i in Items) i.IsSelected = false; }

    [RelayCommand]
    private void Cancel()
    {
        IsOpen = false;
        Items = new ObservableCollection<TagSuggestionItemViewModel>();
    }

    private bool CanApply() => !IsBusy && SelectedCount > 0;

    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyAsync()
    {
        var chosen = Items.Where(i => i.IsSelected)
            .Select(i => new TagChange(i.Suggestion.Track.Id, i.Suggestion.Before, i.Suggestion.After))
            .ToList();
        await RunAsync(chosen, undoing: false);
    }

    [RelayCommand]
    private async Task UndoAsync()
    {
        if (IsBusy || _lastApplied.Count == 0) return;
        await RunAsync(_lastApplied, undoing: true);
    }

    [RelayCommand]
    private void DismissUndo()
    {
        _lastApplied = Array.Empty<TagChange>();
        UndoCount = 0;
    }

    private async Task RunAsync(IReadOnlyList<TagChange> changes, bool undoing)
    {
        IsBusy = true;
        try
        {
            Saving?.Invoke(changes.Select(c => c.TrackId).ToHashSet());

            var progress = new Progress<int>(n => Notified?.Invoke($"Escribiendo etiquetas… {n} de {changes.Count}"));
            var result = undoing
                ? await editor.UndoAsync(changes, progress)
                : await editor.ApplyBatchAsync(changes, progress);

            IsOpen = false;
            Items = new ObservableCollection<TagSuggestionItemViewModel>();
            _lastApplied = undoing ? Array.Empty<TagChange>() : result.Applied;
            UndoCount = _lastApplied.Count;

            Applied?.Invoke(result.Snapshot);

            var done = Plural.Of(result.Applied.Count, undoing ? "canción restaurada" : "canción actualizada",
                                 undoing ? "canciones restauradas" : "canciones actualizadas");
            Notified?.Invoke(result.Errors.Count == 0
                ? done
                : $"{done}; {Plural.Of(result.Errors.Count, "error", "errores")}: {result.Errors[0]}");
        }
        catch (Exception ex)
        {
            Notified?.Invoke($"No se pudo completar: {ex.Message}");
        }
        finally { IsBusy = false; }
    }
}