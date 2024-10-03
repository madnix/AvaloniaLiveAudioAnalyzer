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
}