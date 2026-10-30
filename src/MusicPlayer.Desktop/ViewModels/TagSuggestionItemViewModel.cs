using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Desktop.ViewModels;

public sealed partial class TagSuggestionItemViewModel(TagSuggestion suggestion, Action changed) : ObservableObject
{
    public TagSuggestion Suggestion { get; } = suggestion;
    public string FileName => Path.GetFileName(Suggestion.Track.FilePath);
    public string ChangesText => string.Join("\n", Suggestion.Describe());

    [ObservableProperty] private bool _isSelected = true;

    partial void OnIsSelectedChanged(bool value) => changed();
}