namespace AvaloniaLiveAudioAnalyzer.Services;

/// <summary>
/// Holds all the information about a single chunk of audio for display in the UI
/// </summary>
/// <param name="Loudness"></param>
/// <param name="ShortTermLufs"></param>
/// <param name="IntegratedLufs"></param>
/// <param name="LoudnessRange"></param>
/// <param name="RealtimeDynamics"></param>
/// <param name="AverageRealtimeDynamics"></param>
/// <param name="MomentaryMaxLufs"></param>
/// <param name="ShortTermMaxLufs"></param>
/// <param name="TruePeakMax"></param>
public record AudioChunkData (
    double Loudness,
    double ShortTermLufs,
    double IntegratedLufs,
    double LoudnessRange,
    double RealtimeDynamics,
    double AverageRealtimeDynamics,
    double MomentaryMaxLufs,
    double ShortTermMaxLufs,
    double TruePeakMax
    );