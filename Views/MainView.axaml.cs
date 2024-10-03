using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaLiveAudioAnalyzer.ViewModels;

namespace AvaloniaLiveAudioAnalyzer.Views;

public partial class MainView : UserControl
{
    #region Private Members
    
    /// <summary>
    /// The main view model of this view
    /// </summary>
    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private readonly Control _channelConfigPopup;
    private readonly Control _channelConfigButton;
    private readonly Control _mainGrid;
    private readonly Control _volumeContainer;
    private readonly Control _volumeBar;
    
    /// <summary>
    /// The timeout timer to detect when auto-sizing has finished firing
    /// </summary>
    private readonly Timer _sizingTimer;

    #endregion

    #region Constructor
    
    public MainView()
    {
        InitializeComponent();
        
        _sizingTimer = new Timer(_ =>
        {
            Dispatcher.UIThread.InvokeAsync(UpdateSize);
        });

        _channelConfigButton = this.FindControl<Control>("ChannelConfigurationButton") ??
                               throw new Exception("Cannot find Channel Configuration Button by name");
        _channelConfigPopup = this.FindControl<Control>("ChannelConfigurationPopup") ??
                              throw new Exception("Cannot find Channel Configuration Popup by name");
        _mainGrid = this.FindControl<Control>("MainGrid") ??
                    throw new Exception("Cannot find Main Grid by name");
        _volumeContainer = this.FindControl<Control>("VolumeContainer") ??
                           throw new Exception("Cannot find Volume Container by name");
        _volumeBar = this.FindControl<Control>("VolumeBar") ??
                           throw new Exception("Cannot find Volume Bar by name");
    }

    #endregion

    /// <summary>
    /// Updates the application window/control sizes dynamically
    /// </summary>
    private void UpdateSize()
    {
        if (ViewModel == null) return;
        ViewModel.VolumeContainerHeight = _volumeContainer.Bounds.Height;
        ViewModel.VolumeBarHeight = _volumeBar.Bounds.Height;
    }
    
    /// <summary>
    /// Run on-load initialization code
    /// </summary>
    /// <param name="e"></param>
    protected override async void OnLoaded(RoutedEventArgs e)
    {
        if (ViewModel != null) await ViewModel.LoadCommand.ExecuteAsync(null);
        
        base.OnLoaded(e);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        _sizingTimer.Change(100, int.MaxValue);

        // Get relative position of button, in relation to main grid
        var position = _channelConfigButton.TranslatePoint(new Point(), _mainGrid) ?? 
                       throw new Exception("Cannot get TranslatePoint from Configuration Button");
        
        // Set margin of popup, so it appears bottom left of button
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            _channelConfigPopup.Margin = new Thickness(
                position.X,
                0,
                0,
                _mainGrid.Bounds.Height - position.Y - _channelConfigButton.Bounds.Height
            );
        });
    }

    private void InputElement_OnPointerPressed(object sender, PointerPressedEventArgs e)
    {
        ViewModel?.ChannelConfigurationButtonPressedCommand.Execute(null);
    }
}