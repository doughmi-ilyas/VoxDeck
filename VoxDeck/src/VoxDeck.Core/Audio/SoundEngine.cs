using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace VoxDeck.Core.Audio;

// One shared-mode output stream on the default device; every playing sound is a mixer input.
// Decoding happens on the caller's thread so the audio callback only copies samples.
public sealed class SoundEngine : IDisposable
{
    const int MaxVoices = 16;
    const long CacheBudgetSamples = 8_000_000; // ~32 MB of float samples
    static readonly TimeSpan MaxCachedLength = TimeSpan.FromSeconds(30);

    readonly MMDevice _device;
    readonly WasapiOut _out;
    readonly MixingSampleProvider _mixer;
    readonly VolumeSampleProvider _master;
    readonly WaveFormat _format;
    readonly Dictionary<string, float[]> _cache = new(StringComparer.OrdinalIgnoreCase);
    long _cachedSamples;
    int _voices;
    int _generation;

    public SoundEngine()
    {
        using var devices = new MMDeviceEnumerator();
        _device = devices.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
        _format = WaveFormat.CreateIeeeFloatWaveFormat(_device.AudioClient.MixFormat.SampleRate, 2);
        _mixer = new MixingSampleProvider(_format) { ReadFully = true };
        _mixer.MixerInputEnded += (_, e) =>
        {
            Interlocked.Decrement(ref _voices);
            (e.SampleProvider as IDisposable)?.Dispose();
        };
        _master = new VolumeSampleProvider(_mixer);
        // The stream runs even when silent; pause it while idle if idle CPU measures above budget.
        _out = new WasapiOut(_device, AudioClientShareMode.Shared, true, 50);
        _out.Init(_master);
        _out.Play();
    }

    public float MasterVolume { get => _master.Volume; set => _master.Volume = value; }

    internal int Generation => Volatile.Read(ref _generation);

    /// <summary>Returns null when every voice is busy.</summary>
    public Playback? Play(string path, TimeSpan from, TimeSpan loopStart, float volume, bool loop)
    {
        // New sounds are dropped when all voices are busy; steal the oldest if users notice.
        if (Volatile.Read(ref _voices) >= MaxVoices) return null;
        var playback = CreatePlayback(path);
        playback.LoopStart = loopStart;
        playback.Volume = volume;
        playback.Loop = loop;
        playback.Position = from;
        Interlocked.Increment(ref _voices);
        _mixer.AddMixerInput(playback);
        return playback;
    }

    // Playbacks see the new generation on the next callback and end themselves.
    public void StopAll() => Interlocked.Increment(ref _generation);

    Playback CreatePlayback(string path)
    {
        if (_cache.TryGetValue(path, out var cached)) return new Playback(this, Generation, _format, cached);

        var reader = new AudioFileReader(path);
        try
        {
            var source = ToMixFormat(reader);
            // Long files stream from disk inside the audio callback; move reads to a buffered thread if they glitch.
            if (reader.TotalTime > MaxCachedLength) return new Playback(this, Generation, _format, reader, source);

            var samples = ReadAll(source);
            reader.Dispose();
            // The whole cache is dropped when over budget; LRU if the hit rate matters.
            if (_cachedSamples + samples.Length > CacheBudgetSamples)
            {
                _cache.Clear();
                _cachedSamples = 0;
            }
            _cache[path] = samples;
            _cachedSamples += samples.Length;
            return new Playback(this, Generation, _format, samples);
        }
        catch
        {
            reader.Dispose();
            throw;
        }
    }

    ISampleProvider ToMixFormat(ISampleProvider source)
    {
        source = source.WaveFormat.Channels switch
        {
            1 => new MonoToStereoSampleProvider(source),
            2 => source,
            var n => throw new NotSupportedException($"{n}-channel audio isn't supported yet."),
        };
        return source.WaveFormat.SampleRate == _format.SampleRate
            ? source
            : new WdlResamplingSampleProvider(source, _format.SampleRate);
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
        _out.Dispose();
        _device.Dispose();
    }
}
