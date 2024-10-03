using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using AvaloniaLiveAudioAnalyzer.DataModels;
using AvaloniaLiveAudioAnalyzer.Services;
using CommunityToolkit.Mvvm.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Threading;
using AvaloniaLiveAudioAnalyzer.ExtendedClass;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace AvaloniaLiveAudioAnalyzer.ViewModels;

public partial class MainViewModel : ObservableObject
{
    #region Private Members

    /// <summary>
    /// The audio capture service
    /// </summary>
    private IAudioCaptureService _audioCaptureService;

    /// <summary>
    /// A slow tick counter to update the text slower than the graphs and bars
    /// </summary>
    private int _updateCounter;
    
    #endregion
    
    #region Private ObservableProperty

    [ObservableProperty]
    private string? _appTitle = "MADNIX";
    
    [ObservableProperty]
    private string? _regularTitle = "LOUDNESS METER";
    
    [ObservableProperty]
    private string? _shortTermLoudness = "0 LUFS";
    
    [ObservableProperty]
    private string? _integratedLoudness = "0 LUFS";
    
    [ObservableProperty]
    private string? _loudnessRange = "0 LU";
    
    [ObservableProperty]
    private string? _realtimeDynamics = "0 LU";
    
    [ObservableProperty]
    private string? _averageDynamics = "0 LU";
    
    [ObservableProperty]
    private string? _momentaryMaxLoudness = "0 LUFS";
    
    [ObservableProperty]
    private string? _shortTermMaxLoudness = "0 LUFS";
    
    [ObservableProperty]
    private string? _truePeakMax = "0 dB";

    [ObservableProperty]
    private bool _channelConfigurationListIsOpen;
    
    [ObservableProperty]
    private double _volumePercentPosition;
    
    [ObservableProperty]
    private double _volumeContainerHeight;

    [ObservableProperty]
    private double _volumeBarHeight;

    [ObservableProperty]
    private double _volumeBarMaskHeight;
    
    [ObservableProperty]
    private ObservableGroupedCollection<string, ChannelConfigurationItem> _channelConfigurations = default!;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChannelConfigurationButtonText))]
    private ChannelConfigurationItem? _selectedChannelConfiguration;
    
    private readonly ObservableCollection<ObservableValue> _mainChartValues = [];
    #endregion

    #region Public Properties
    
    public string ChannelConfigurationButtonText => SelectedChannelConfiguration?.ShortText ?? "Select Channel";

    public ISeries[] Series { get; set; }

    public List<Axis> YAxis { get; set; } =
    [
        new()
        {
            MinStep = 1,
            ForceStepToMin = true,
            MinLimit = 0,
            MaxLimit = 60,
            Labeler = val => (Math.Min(60, Math.Max(0, val)) - 60).ToString(CultureInfo.InvariantCulture),
            IsVisible = false
            //IsInverted = true
        }
    ];
    
    #endregion
    
    #region Public Commands

    /// <summary>
    /// Youtube url open
    /// </summary>
    /// <param name="url"></param>
    [RelayCommand]
    private void OpenUrl(string url)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
        }
        catch (Exception ex)
        {
            // 에러 처리
            Console.WriteLine($"URL을 열 수 없습니다: {ex.Message}");
        }
    }
    
    [RelayCommand]
    private void ChannelConfigurationButtonPressed() => ChannelConfigurationListIsOpen = true;

    [RelayCommand]
    private void ChannelConfigurationItemPressed(ChannelConfigurationItem channelConfigurationItem)
    {
        // Update the selected item
        SelectedChannelConfiguration = channelConfigurationItem;
        
        // Close the menu
        ChannelConfigurationListIsOpen = false;
    }

    /// <summary>
    /// Do initial loading of data and settings up services
    /// </summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        // Get the channel configuration data
        var channelConfigurations = await _audioCaptureService.GetChannelConfigurationsAsync();
        
        // Create a grouping from the flat data
        ChannelConfigurations = new ObservableGroupedCollection<string, ChannelConfigurationItem>(
            channelConfigurations.GroupBy(item => item.Group));

        StartCapture();
    }
    
    #endregion
    
    #region Constructor
    
    /// <summary>
    /// Default constructor
    /// </summary>
    /// <param name="audioCaptureService">The audio interface service</param>
    public MainViewModel(IAudioCaptureService audioCaptureService)
    {
        _audioCaptureService = audioCaptureService;

        Initialize();
    }
    
    /// <summary>
    /// Design-time constructor
    /// </summary>
    public MainViewModel()
    {
        _audioCaptureService = new BassAudioCaptureService(new byte[256]);
        
        Initialize();
    }
    
    #endregion

    /// <summary>
    /// Initializes MainChartValues member and Series property
    /// </summary>
    private void Initialize()
    {
        //_mainChartValues.AddRange(Enumerable.Range(0, 170).Select(x => new ObservableValue(0)));
        
        for (var i = 0; i <= 170; i++)
            _mainChartValues.Add(new ObservableValue(0));
        
        Series =
        [
            new LineSeries<ObservableValue>
            {
                Values = _mainChartValues,
                GeometrySize = 0,
                GeometryStroke = null,
                Fill = new SolidColorPaint(new SKColor(63, 77, 99)),
                Stroke = new SolidColorPaint(new SKColor(120, 152, 203))
                {
                    StrokeThickness = 3
                }
            }
        ];
        
        // Temp code to move volume position
        
        /*var tick = 0;
        double input;

        var tempTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1 / 60.0)
        };

        tempTimer.Tick += (o, e) =>
        {
            tick++;

            // Show down ticks
            input = tick / 20f;

            // Scale volume
            var scale = VolumeContainerHeight / 2f;

            VolumePercentPosition = (Math.Sin(input) + 1) * scale;
        };

        tempTimer.Start();*/
    }

    /// <summary>
    /// Starts capturing audio from the specified device
    /// </summary>
    private void StartCapture()
    {
        // Initialize capturing an specific device
        _audioCaptureService.InitCapture();
        
        // Listen out for chunks of information
        _audioCaptureService.AudioChunkAvailable += ProcessAudioChunk;

        // Start capturing
        _audioCaptureService.Start();
    }

    private void ProcessAudioChunk(AudioChunkData audioChunkData)
    {
        // Counter between 0-1-2
        _updateCounter = (_updateCounter + 1) % 3;

        // Every time counter is at 0...
        if (_updateCounter == 0)
        {
            ShortTermLoudness = $"{Math.Max(-60, audioChunkData.ShortTermLufs):0.0} LUFS";
            IntegratedLoudness = $"{Math.Max(-60, audioChunkData.IntegratedLufs):0.0} LUFS";
            LoudnessRange = $"{Math.Max(-60, audioChunkData.LoudnessRange):0.0} LU";
            RealtimeDynamics = $"{Math.Max(-60, audioChunkData.RealtimeDynamics):0.0} LU";
            AverageDynamics = $"{Math.Max(-60, audioChunkData.AverageRealtimeDynamics):0.0} LU";
            MomentaryMaxLoudness = $"{Math.Max(-60, audioChunkData.MomentaryMaxLufs):0.0} LUFS";
            ShortTermMaxLoudness = $"{Math.Max(-60, audioChunkData.ShortTermMaxLufs):0.0} LUFS";
            TruePeakMax = $"{Math.Max(-60, audioChunkData.TruePeakMax):0.0} dB";

            Dispatcher.UIThread.Invoke(() =>
            {
                _mainChartValues.RemoveAt(0);
                _mainChartValues.Add(new ObservableValue(Math.Max(0, 60 + audioChunkData.ShortTermLufs)));
            });
        }

        // Set volume bar height
        VolumeBarMaskHeight = Math.Min(VolumeBarHeight, VolumeBarHeight / 60 * -audioChunkData.Loudness);

        // Set volume Arrow height
        VolumePercentPosition = Math.Min(VolumeContainerHeight, VolumeContainerHeight / 60 * -audioChunkData.ShortTermLufs);
    }
}