using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicPlayer.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _greeting = "MusicPlayer – listo para desarrollo";
}
