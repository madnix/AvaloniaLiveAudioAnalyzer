using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaLiveAudioAnalyzer.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string? _appTitle = "AVALONIA";
    
    [ObservableProperty]
    private string? _regularTitle = "LOUDNESS METER";

    /*public MainWindowViewModel()
    {
        Task.Run(async () =>
            {
                await Task.Delay(2000);
                AppTitle = "MADNIX";
            }
        );
    }*/
}