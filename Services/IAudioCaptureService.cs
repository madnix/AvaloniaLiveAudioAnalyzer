using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AvaloniaLiveAudioAnalyzer.DataModels;

namespace AvaloniaLiveAudioAnalyzer.Services;

public interface IAudioCaptureService
{
    /// <summary>
    /// Fetch the channel configurations
    /// </summary>
    /// <returns></returns>
    Task<List<ChannelConfigurationItem>> GetChannelConfigurationsAsync();

    /// <summary>
    /// Initialises capturing of audio from a specific device
    /// </summary>
    /// <param name="frequency">The frequency</param>
    void InitCapture(int frequency = 44100);

    /// <summary>
    /// Start capturing audio
    /// </summary>
    void Start();
    
    /// <summary>
    /// Stop capturing audio
    /// </summary>
    void Stop();
    
    /// <summary>
    /// A callback for when the next chunk of audio data is available
    /// </summary>
    public event Action<AudioChunkData>? AudioChunkAvailable;
    
    /// <summary>
    /// Device Number
    /// </summary>
    public int DeviceId { get; }
}