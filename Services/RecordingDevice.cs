using System;
using System.Collections.Generic;
using ManagedBass;

namespace AvaloniaLiveAudioAnalyzer.Services;

public class RecordingDevice : IDisposable
{
    private int Index { get; }

    private string Name { get; }

    RecordingDevice(int index, string name)
    {
        Index = index;

        Name = name;
    }

    public static IEnumerable<RecordingDevice> Enumerate()
    {
        for (var i = 0; Bass.RecordGetDeviceInfo(i, out var info); ++i)
            yield return new RecordingDevice(i, info.Name);
    }

    public void Dispose()
    {
        Bass.CurrentRecordingDevice = Index;
        Bass.RecordFree();
    }

    public override string ToString() => Name;
}