using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AvaloniaLiveAudioAnalyzer.DataModels;
using ManagedBass;
using NWaves.Signals;
using NWaves.Utils;

namespace AvaloniaLiveAudioAnalyzer.Services;

public class BassAudioCaptureService : IDisposable, IAudioCaptureService
{
    #region Private Members
    
    /// <summary>
    /// The buffer for a short capture of microphone audio
    /// </summary>
    private byte[] _buffer;

    /// <summary>
    /// The device ID we want to capture 
    /// </summary>
    private int _deviceId;

    /// <summary>
    /// The handle to the device we want to capture
    /// </summary>
    private int _handle;
    
    /// <summary>
    /// The last few sets of captured audio bytes, converted to LUFS
    /// </summary>
    private readonly Queue<double> _lufs = new();
    
    /// <summary>
    /// The last few sets of captured audio bytes, converted to LUFS
    /// </summary>
    private readonly Queue<double> _lufsLonger = new();
    
    /// <summary>
    /// The frequency to capture at
    /// </summary>
    private readonly int _captureFrequency = 44100;
    
    #endregion
    
    #region Public Events
    
    /// <inheritdoc />
    public event Action<AudioChunkData>? AudioChunkAvailable;
    
    #endregion
    
    #region Public properties
    
    public int DeviceId => _deviceId;
        
    #endregion

    #region Default Constructor
    
    /// <summary>
    /// Initializes the audio capture service, and starts capturing the specified device ID 
    /// </summary>
    /// <param name="buffer"></param>
    public BassAudioCaptureService(byte[] buffer)
    {
        _buffer = buffer;
        
        // Initialize and start
        Bass.Init();
        
        // Output all devices, then select one
        /*foreach (var device in RecordingDevice.Enumerate())
            Console.WriteLine($"{device.Index}: {device.Name}");

        var outputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MBass");
        Directory.CreateDirectory(outputPath);

        var filePath = Path.Combine(outputPath, DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".wav");

        using var writer =
            new WaveFileWriter(
                new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read),
                new WaveFormat());*/
    }

    /// <inheritdoc />
    public void InitCapture(int frequency = 44100)
    {
        // Store device ID and Buffer
        _deviceId = FindMicrophoneDeviceNumber();
        
        try
        {
            Bass.RecordFree();
        }
        catch
        {
            // ignored
        }
        
        // Initialize new device
        Bass.RecordInit(_deviceId);

        // Start recording (but in a paused state)
        _handle = Bass.RecordStart(frequency, 2, BassFlags.RecordPause, 20, AudioChunkCaptured);
    }
    
    #endregion

    #region Channel Configuration Methods
    
    public Task<List<ChannelConfigurationItem>> GetChannelConfigurationsAsync() =>
        Task.FromResult(new List<ChannelConfigurationItem>(new[]
        {
            new ChannelConfigurationItem("mono Stereo Configuration", "Mono", "Mono"),
            new ChannelConfigurationItem("mono Stereo Configuration", "Stereo", "Stereo"),
            new ChannelConfigurationItem("5.1 Surround", "5.1 DTS - (L, R, Ls, Rs, C, LFE)", "5.1 DTS"),
            new ChannelConfigurationItem("5.1 ITU", "5.1 DTS - (L, R, C, LFE, Ls, Rs)", "5.1 ITU"),
            new ChannelConfigurationItem("5.1 FILM", "5.1 DTS - (L, C, R, Ls, Rs, LFE)", "5.1 FILM")
        }));
    
    #endregion

    #region Capture Audio Methods
    
    /// <summary>
    /// Call back from the audio recording, to process each chunk of audio data
    /// </summary>
    /// <param name="handle"></param>
    /// <param name="buffer"></param>
    /// <param name="length"></param>
    /// <param name="user"></param>
    /// <returns></returns>
    private bool AudioChunkCaptured(int handle, IntPtr buffer, int length, IntPtr user)
    {
        if (_buffer.Length < length)
            _buffer = new byte[length];

        Marshal.Copy(buffer, _buffer, 0, length);

        // Calculate useful information from this chunk
        CalculateValues(_buffer);

        return true;
    }

    /// <summary>
    /// Calculates usable information from an audio chunk
    /// </summary>
    /// <param name="buffer">The audio buffer</param>
    private void CalculateValues(byte[] buffer)
    {
        // Get total PCM16 samples in this buffer (16 bits per sample)
        var sampleCount = buffer.Length / 2;
        
        // Create our Discrete Signal ready to be filled with information
        var signal = new DiscreteSignal(_captureFrequency, sampleCount);
        
        // Loop all bytes and extract the 16 bits, into signal floats
        using var reader = new BinaryReader(new MemoryStream(buffer));
        
        for (var i = 0; i < sampleCount; i++)
            signal[i] = reader.ReadInt16() / 32768f;
        
        // Calculate the LUFS
        var lufs = Scale.ToDecibel(signal.Rms() * 1.2);
        _lufs.Enqueue(lufs);
        _lufsLonger.Enqueue(lufs);
        
        // Limit queue sizes
        if (_lufs.Count > 10)
            _lufs.Dequeue();
        if (_lufsLonger.Count > 200)
            _lufsLonger.Dequeue();

        // Calculate the average
        var averageLufs = _lufs.Average();
        var averageLongLufs = _lufsLonger.Average();

        // Fire off this chunk of information to listeners
        AudioChunkAvailable?.Invoke(new AudioChunkData
        (
            // TODO: Make these calculations correct
            ShortTermLufs: averageLongLufs,
            Loudness: averageLufs,
            LoudnessRange: averageLufs + averageLufs * 0.9,
            RealtimeDynamics: averageLufs + averageLufs * 0.8,
            AverageRealtimeDynamics: averageLufs + averageLufs * 0.7,
            TruePeakMax: averageLufs + averageLufs * 0.6,
            IntegratedLufs: averageLufs + averageLufs * 0.5,
            MomentaryMaxLufs: averageLufs + averageLufs * 0.4,
            ShortTermMaxLufs: averageLufs + averageLufs * 0.3
        ));
    }
    
    /// <summary>
    /// Find Microphone Device number
    /// </summary>
    /// <returns></returns>
    private int FindMicrophoneDeviceNumber()
    {
        // Bass 초기화
        /*if (!Bass.Init(-1))
        {
            Console.WriteLine($"Bass 초기화 실패: {Bass.LastError}");
            return -1;
        }*/

        try
        {
            // 모든 입력 장치 순회
            for (var i = 0; i < Bass.RecordingDeviceCount; i++)
            {
                var info = Bass.RecordGetDeviceInfo(i);
                Console.WriteLine($"장치 {i}: {info.Name}, 활성화: {info.IsEnabled}, 기본: {info.IsDefault}, 타입: {info.Type}");

                switch (info.IsEnabled)
                {
                    // 마이크로폰 장치 발견, 실제로 사용 가능한지 테스트
                    case true when info.Type == DeviceType.Microphone &&
                                   Bass.RecordInit(i) &&
                                   info.IsDefault:
                        Bass.RecordFree();
                        return i;
                }
            }
        }
        finally
        {
            // Bass 해제
            Bass.Free();
        }

        // 마이크로폰을 찾지 못한 경우
        return -1;
    }
    #endregion

    /// <inheritdoc />
    public void Start()
    {
        Bass.ChannelPlay(_handle);
    }

    /// <inheritdoc />
    public void Stop()
    {
        Bass.ChannelStop(_handle);
    }

    public void Dispose()
    {
        Bass.CurrentRecordingDevice = _deviceId;

        Bass.RecordFree();
    }
    

    // private const int SampleRate = 44100;
    // private const int Channels = 2;
    // private readonly CancellationTokenSource? _cts = cts;
    // private bool _recordingDevice;

    /*public void CaptureDefaultInput()
    {
        // BASS 초기화
        if (!Bass.Init(-1, SampleRate, DeviceInitFlags.Default))
        {
            Console.WriteLine("BASS 초기화 실패");
            return;
        }

        // 마이크로폰 입력 활성화
        _recordingDevice = Bass.RecordInit(-1);
        if (_recordingDevice == false)
        {
            Console.WriteLine("마이크로폰 초기화 실패");
            Bass.Free();
            return;
        }

        var recordStream = Bass.RecordStart(SampleRate, Channels, BassFlags.RecordPause, null);

        if (recordStream == 0)
        {
            Console.WriteLine($"녹음 시작 실패: {Bass.LastError}");
            Bass.RecordFree();
            Bass.Free();
            return;
        }

        Bass.ChannelPlay(recordStream);

        Console.WriteLine("마이크로폰 캡처 중... (종료하려면 아무 키나 누르세요)");

        // 오디오 레벨 모니터링 시작
        var monitorThread = new Thread(() =>
        {
            if (_cts is not null) MonitorAudioLevel(_cts.Token, recordStream);
        });
        monitorThread.Start();

        // 사용자 입력 대기
        Console.ReadKey();

        // 모니터링 중지
        _cts.Cancel();
        monitorThread.Join(); // 쓰레드가 완전히 종료될 때까지 대기

        // 녹음 중지 및 정리
        Bass.ChannelStop(recordStream);
        Bass.StreamFree(recordStream);
        Bass.RecordFree();
        Bass.Free();

        Console.WriteLine("캡처 종료");
        _cts.Dispose();
    }*/

    /*private void MonitorAudioLevel(CancellationToken token, int stream)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                float level = Bass.ChannelGetLevel(stream);
                var bars = (int)(level * 50); // 50은 콘솔에 표시할 최대 바 수

                Console.Write($"\rAudio Level: [{new string('#', bars),-50}] {level * 100:F2}%");

                // 취소 요청 확인 및 짧은 대기
                if (token.WaitHandle.WaitOne(100))
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 취소 요청 처리
        }
        finally
        {
            Console.WriteLine("\n모니터링 종료");
        }
    }*/
}