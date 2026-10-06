using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Visualizer.App.Audio;

public sealed class AudioCaptureService : IDisposable
{
    private readonly AudioRingBuffer _buffer;
    private readonly Action<string> _log;
    private readonly MMDeviceEnumerator _enumerator = new();
    private WasapiLoopbackCapture? _capture;
    private MMDevice? _device;
    private float[] _convertBuffer = new float[8192];
    private string? _selectedDeviceId;
    private bool _stopping;
    private long _lastSignalTick;
    private long _captureStartedTick;
    public bool IsCapturing => _capture is not null;
    public bool HasRecentSignal => Environment.TickCount64 - Interlocked.Read(ref _lastSignalTick) < 1500;
    public bool IsWaitingForAudio => IsCapturing && !HasRecentSignal && Environment.TickCount64 - _captureStartedTick > 3000;

    public AudioCaptureService(AudioRingBuffer buffer, Action<string> log)
    {
        _buffer = buffer;
        _log = log;
    }

    public int SampleRate { get; private set; } = 48000;
    public string? CurrentDeviceId => _selectedDeviceId;

    public IReadOnlyList<AudioDeviceInfo> GetOutputDevices()
    {
        using var defaultDevice = GetDefaultDevice();
        var devices = new List<AudioDeviceInfo>();
        foreach (var device in _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            using (device)
                devices.Add(new AudioDeviceInfo(device.ID, device.FriendlyName, device.ID == defaultDevice?.ID));
        }
        return devices;
    }

    public void Start(string? deviceId = null)
    {
        Stop();
        using var defaultDevice = deviceId is null ? GetDefaultDevice() : null;
        _selectedDeviceId = deviceId ?? defaultDevice?.ID;
        if (_selectedDeviceId is null)
        {
            _log("No active Windows output device is available.");
            return;
        }

        try
        {
            _device = _enumerator.GetDevice(_selectedDeviceId);
            _capture = new WasapiLoopbackCapture(_device);
            // Decode extensible PCM/float formats as their equivalent standard format.
            if (_capture.WaveFormat is WaveFormatExtensible extensible)
                _capture.WaveFormat = extensible.ToStandardWaveFormat();
            var format = _capture.WaveFormat;
            if (!((format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32) ||
                  (format.Encoding == WaveFormatEncoding.Pcm && format.BitsPerSample == 16)))
                throw new NotSupportedException($"Unsupported audio format: {format}");
            _capture.DataAvailable += OnDataAvailable;
            _capture.RecordingStopped += OnRecordingStopped;
            SampleRate = _capture.WaveFormat.SampleRate;
            _buffer.Clear();
            Interlocked.Exchange(ref _lastSignalTick, Environment.TickCount64 - 10000);
            _captureStartedTick = Environment.TickCount64;
            _capture.StartRecording();
            _log($"Capturing system output: {_device.FriendlyName}");
        }
        catch (Exception ex)
        {
            Stop();
            _log($"Unable to start audio capture: {ex.Message}");
            RecoverToDefaultDevice();
        }
    }

    public void Stop()
    {
        _stopping = true;
        if (_capture is not null)
        {
            _capture.DataAvailable -= OnDataAvailable;
            _capture.RecordingStopped -= OnRecordingStopped;
            try
            {
                _capture.StopRecording();
            }
            catch
            {
            }

            _capture.Dispose();
            _capture = null;
        }

        _device?.Dispose();
        _device = null;
        _buffer.Clear();
        _stopping = false;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        var waveFormat = _capture?.WaveFormat;
        if (waveFormat is null)
        {
            return;
        }

        var channels = Math.Max(1, waveFormat.Channels);
        var bytesPerSample = waveFormat.BitsPerSample / 8;
        var frameCount = e.BytesRecorded / Math.Max(1, bytesPerSample * channels);
        if (_convertBuffer.Length < frameCount)
        {
            _convertBuffer = new float[frameCount * 2];
        }

        if (waveFormat.Encoding == WaveFormatEncoding.IeeeFloat && waveFormat.BitsPerSample == 32)
        {
            ConvertFloat32(e.Buffer, frameCount, channels);
        }
        else if (waveFormat.Encoding == WaveFormatEncoding.Pcm && waveFormat.BitsPerSample == 16)
        {
            ConvertPcm16(e.Buffer, frameCount, channels);
        }
        else
        {
            return;
        }

        _buffer.Write(_convertBuffer.AsSpan(0, frameCount));
        for (var i = 0; i < frameCount; i++)
        {
            if (Math.Abs(_convertBuffer[i]) > 0.00001f)
            {
                Interlocked.Exchange(ref _lastSignalTick, Environment.TickCount64);
                break;
            }
        }
    }

    private void ConvertFloat32(byte[] source, int frameCount, int channels)
    {
        for (var frame = 0; frame < frameCount; frame++)
        {
            var sum = 0f;
            var readableChannels = Math.Min(2, channels);
            for (var channel = 0; channel < readableChannels; channel++)
            {
                var offset = (frame * channels + channel) * sizeof(float);
                sum += BitConverter.ToSingle(source, offset);
            }

            _convertBuffer[frame] = Math.Clamp(sum / readableChannels, -1f, 1f);
        }
    }

    private void ConvertPcm16(byte[] source, int frameCount, int channels)
    {
        for (var frame = 0; frame < frameCount; frame++)
        {
            var sum = 0f;
            var readableChannels = Math.Min(2, channels);
            for (var channel = 0; channel < readableChannels; channel++)
            {
                var offset = (frame * channels + channel) * sizeof(short);
                sum += BitConverter.ToInt16(source, offset) / 32768f;
            }

            _convertBuffer[frame] = Math.Clamp(sum / readableChannels, -1f, 1f);
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (!_stopping && e.Exception is not null)
        {
            _log($"Audio capture stopped: {e.Exception.Message}");
            RecoverToDefaultDevice();
        }
    }

    private MMDevice? GetDefaultDevice()
    {
        try
        {
            return _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }
        catch
        {
            return null;
        }
    }

    private void RecoverToDefaultDevice()
    {
        using var defaultDevice = GetDefaultDevice();
        if (defaultDevice is null || defaultDevice.ID == _selectedDeviceId)
        {
            return;
        }

        _log("Recovering capture on the current default output device.");
        Start(defaultDevice.ID);
    }

    public void Dispose()
    {
        Stop();
        _enumerator.Dispose();
    }
}
