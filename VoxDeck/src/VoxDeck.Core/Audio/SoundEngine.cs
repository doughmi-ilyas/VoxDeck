using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using VoxDeck.Core.Capture;

namespace VoxDeck.Core.Audio;

// Two outputs: the "monitor" (what you hear) and, while voice mixing is on, the "call" — a virtual cable that
// voice apps use as a microphone, carrying your mic plus your sounds. Each sound plays once per output, so the
// two devices never have to share a clock. Decoding happens on the caller's thread; audio callbacks only copy.
public sealed class SoundEngine : IDisposable
{
    const int MaxVoices = 16;
    const long CacheBudgetSamples = 16_000_000; // ~64 MB of float samples
    static readonly TimeSpan MaxCachedLength = TimeSpan.FromSeconds(30);

    readonly Dictionary<(string Path, int Rate), float[]> _cache = [];
    long _cachedSamples;
    int _generation;
    Output? _monitor;
    Output? _call;
    MicInput? _mic;
    float _masterVolume = 1;
    float _callSoundVolume = 1;
    float _micGain = 1;
    bool _micMuted;

    /// <summary>A device stopped working (unplugged, disabled, driver error). Raised on the UI thread.</summary>
    public event Action<string>? Failed;

    internal int Generation => Volatile.Read(ref _generation);

    public string? MonitorDeviceId => _monitor?.Id;
    public string? MonitorName => _monitor?.Name;
    public string? CallDeviceId => _call?.Id;
    public string? MicDeviceId => _mic?.Id;
    public bool MonitorFailed => _monitor?.Failed == true;
    public bool VoiceMixFailed => _call?.Failed == true || _mic?.Failed == true;

    /// <summary>Volume of sounds in your own headphones.</summary>
    public float MasterVolume
    {
        get => _masterVolume;
        set
        {
            _masterVolume = value;
            if (_monitor is not null) _monitor.SoundVolume = value;
        }
    }

    /// <summary>Volume of sounds in the voice chat, independent of what you hear.</summary>
    public float CallSoundVolume
    {
        get => _callSoundVolume;
        set
        {
            _callSoundVolume = value;
            if (_call is not null) _call.SoundVolume = value;
        }
    }

    /// <summary>VoxDeck's own mic gain; Windows' microphone level is never touched.</summary>
    public float MicGain
    {
        get => _micGain;
        set
        {
            _micGain = value;
            if (_mic is not null) _mic.Gain = value;
        }
    }

    public bool MicMuted
    {
        get => _micMuted;
        set
        {
            _micMuted = value;
            if (_mic is not null) _mic.Muted = value;
        }
    }

    /// <summary>Opens (or reopens after a failure) the device you hear sounds on. Null closes it.</summary>
    public void SetMonitorDevice(string? deviceId)
    {
        if (deviceId == _monitor?.Id && !MonitorFailed) return;
        _monitor?.Dispose();
        _monitor = null;
        if (deviceId is null) return;
        _monitor = new Output(deviceId, OnFailed) { SoundVolume = _masterVolume };
    }

    /// <summary>Starts sending the mic plus every new sound to the virtual cable.</summary>
    public void StartVoiceMix(string cableDeviceId, string micDeviceId)
    {
        StopVoiceMix();
        var call = new Output(cableDeviceId, OnFailed) { SoundVolume = _callSoundVolume };
        try
        {
            _mic = new MicInput(micDeviceId, call.Format.SampleRate, OnFailed) { Gain = _micGain, Muted = _micMuted };
            call.AddInput(_mic.Output);
            _call = call;
        }
        catch
        {
            call.Dispose();
            throw;
        }
    }

    public void StopVoiceMix()
    {
        _mic?.Dispose();
        _call?.Dispose();
        _mic = null;
        _call = null;
    }

    public float TakeMonitorPeak() => _monitor?.TakePeak() ?? 0;
    public float TakeCallPeak() => _call?.TakePeak() ?? 0;
    public float TakeMicPeak() => _mic?.TakePeak() ?? 0;

    /// <summary>
    /// Returns null when every voice is busy. Throws InvalidOperationException with a readable message
    /// when the route has nowhere to play (no output device, or voice chat only while mixing is off).
    /// </summary>
    public Playback? Play(string path, TimeSpan from, TimeSpan loopStart, float volume, bool loop, SoundRoute route = SoundRoute.Both)
    {
        var outputs = SoundRoutes.Pick(route, _monitor, _call);
        if (outputs.Length == 0)
            throw new InvalidOperationException(route == SoundRoute.CallOnly
                ? "This sound only plays in voice chat, and voice mixing is off. Turn on “Send to voice chat” in the Mixer."
                : "No sound output device is open.");
        // New sounds are dropped when all voices are busy; steal the oldest if users notice.
        if (outputs[0].SoundCount >= MaxVoices) return null;

        var voices = new Voice[outputs.Length];
        try
        {
            for (var i = 0; i < outputs.Length; i++)
            {
                var voice = CreateVoice(path, outputs[i].Format);
                voice.LoopStart = loopStart;
                voice.Volume = volume;
                voice.Loop = loop;
                voice.Position = from;
                voices[i] = voice;
            }
        }
        catch
        {
            foreach (var voice in voices) voice?.Dispose();
            throw;
        }
        for (var i = 0; i < outputs.Length; i++) outputs[i].AddSound(voices[i]);
        return new Playback(voices);
    }

    // Playbacks see the new generation on the next callback and end themselves.
    public void StopAll() => Interlocked.Increment(ref _generation);

    /// <summary>A short, quiet chime so people can check which device they hear.</summary>
    public void PlayTestTone()
    {
        if (_monitor is null) return;
        var rate = _monitor.Format.SampleRate;
        var frames = rate * 35 / 100;
        var fade = rate * 0.02;
        var samples = new float[frames * 2];
        for (var i = 0; i < frames; i++)
        {
            var envelope = Math.Min(1, Math.Min(i, frames - i) / fade);
            samples[2 * i] = samples[2 * i + 1] = (float)(0.2 * envelope * Math.Sin(2 * Math.PI * 880 * i / rate));
        }
        _monitor.AddSound(new Voice(this, Generation, _monitor.Format, samples));
    }

    /// <summary>Plays recorded stereo samples (any rate) on the monitor, e.g. a voice-chat test recording.</summary>
    public void PlayClip(float[] samples, int sampleRate)
    {
        if (_monitor is null) return;
        var format = _monitor.Format;
        var clip = sampleRate == format.SampleRate
            ? samples
            : ReadAll(new WdlResamplingSampleProvider(new Voice(null, 0, WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2), samples), format.SampleRate));
        _monitor.AddSound(new Voice(this, Generation, format, clip));
    }

    void OnFailed(string message) => Failed?.Invoke(message);

    Voice CreateVoice(string path, WaveFormat format)
    {
        var key = (path.ToUpperInvariant(), format.SampleRate);
        if (_cache.TryGetValue(key, out var cached)) return new Voice(this, Generation, format, cached);

        var reader = new AudioFileReader(path);
        try
        {
            var source = ToFormat(reader, format.SampleRate);
            // Long files stream from disk inside the audio callback; move reads to a buffered thread if they glitch.
            if (reader.TotalTime > MaxCachedLength) return new Voice(this, Generation, format, reader, source);

            var samples = ReadAll(source);
            reader.Dispose();
            // The whole cache is dropped when over budget; LRU if the hit rate matters.
            if (_cachedSamples + samples.Length > CacheBudgetSamples)
            {
                _cache.Clear();
                _cachedSamples = 0;
            }
            _cache[key] = samples;
            _cachedSamples += samples.Length;
            return new Voice(this, Generation, format, samples);
        }
        catch
        {
            reader.Dispose();
            throw;
        }
    }

    static ISampleProvider ToFormat(ISampleProvider source, int sampleRate)
    {
        source = source.WaveFormat.Channels switch
        {
            1 => new MonoToStereoSampleProvider(source),
            2 => source,
            var n => throw new NotSupportedException($"{n}-channel audio isn't supported yet."),
        };
        return source.WaveFormat.SampleRate == sampleRate ? source : new WdlResamplingSampleProvider(source, sampleRate);
    }

    static float[] ReadAll(ISampleProvider source)
    {
        var all = new List<float>();
        var buffer = new float[source.WaveFormat.SampleRate * source.WaveFormat.Channels];
        int n;
        while ((n = source.Read(buffer, 0, buffer.Length)) > 0) all.AddRange(buffer.AsSpan(0, n));
        return [.. all];
    }

    public void Dispose()
    {
        StopVoiceMix();
        _monitor?.Dispose();
        _monitor = null;
    }
}
