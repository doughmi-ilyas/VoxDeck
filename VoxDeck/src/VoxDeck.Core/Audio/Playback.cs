using NAudio.Wave;

namespace VoxDeck.Core.Audio;

// One playing instance of a sound. Read runs on the audio thread; everything else is safe to call from the UI.
public sealed class Playback : ISampleProvider, IDisposable
{
    readonly SoundEngine? _engine; // null in tests
    readonly int _generation;
    readonly float[]? _samples;        // decoded in memory
    readonly AudioFileReader? _reader; // streamed from disk
    readonly ISampleProvider? _stream;
    readonly int _samplesPerSecond;
    long _position;
    volatile bool _stopped, _ended, _paused, _loop;
    volatile float _volume = 1;

    internal Playback(SoundEngine? engine, int generation, WaveFormat format, float[] samples)
        : this(engine, generation, format)
    {
        _samples = samples;
        Duration = TimeSpan.FromSeconds((double)samples.Length / _samplesPerSecond);
    }

    internal Playback(SoundEngine engine, int generation, WaveFormat format, AudioFileReader reader, ISampleProvider stream)
        : this(engine, generation, format)
    {
        _reader = reader;
        _stream = stream;
        Duration = reader.TotalTime;
    }

    Playback(SoundEngine? engine, int generation, WaveFormat format)
    {
        _engine = engine;
        _generation = generation;
        WaveFormat = format;
        _samplesPerSecond = format.SampleRate * format.Channels;
    }

    public WaveFormat WaveFormat { get; }
    public TimeSpan Duration { get; }
    public TimeSpan LoopStart { get; set; }
    public bool Ended => _ended;
    public bool Paused { get => _paused; set => _paused = value; }
    public bool Loop { get => _loop; set => _loop = value; }
    public float Volume { get => _volume; set => _volume = value; }

    public TimeSpan Position
    {
        get
        {
            if (_ended) return TimeSpan.Zero;
            if (_samples is not null) return TimeSpan.FromSeconds((double)Interlocked.Read(ref _position) / _samplesPerSecond);
            try
            {
                return _reader!.CurrentTime;
            }
            catch (ObjectDisposedException)
            {
                return TimeSpan.Zero;
            }
        }
        set
        {
            var t = value < TimeSpan.Zero ? TimeSpan.Zero : value > Duration ? Duration : value;
            if (_samples is not null)
                Interlocked.Exchange(ref _position, (long)(t.TotalSeconds * WaveFormat.SampleRate) * WaveFormat.Channels);
            else if (!_ended)
                _reader!.CurrentTime = t;
        }
    }

    public void Stop() => _stopped = true;

    public int Read(float[] buffer, int offset, int count)
    {
        if (_stopped || (_engine is not null && _engine.Generation != _generation))
        {
            _ended = true;
            return 0;
        }
        if (_paused)
        {
            Array.Clear(buffer, offset, count);
            return count;
        }

        var n = ReadSource(buffer, offset, count);
        while (n < count && _loop)
        {
            Position = LoopStart;
            var more = ReadSource(buffer, offset + n, count - n);
            if (more == 0) break;
            n += more;
        }
        if (n < count) _ended = true; // the mixer drops inputs that return a short read

        var volume = _volume;
        if (volume != 1)
            for (var i = offset; i < offset + n; i++) buffer[i] *= volume;
        return n;
    }

    int ReadSource(float[] buffer, int offset, int count)
    {
        if (_samples is null) return _stream!.Read(buffer, offset, count);
        var position = Interlocked.Read(ref _position);
        var n = (int)Math.Clamp(_samples.Length - position, 0, count);
        Array.Copy(_samples, position, buffer, offset, n);
        Interlocked.CompareExchange(ref _position, position + n, position); // a seek made meanwhile wins
        return n;
    }

    public void Dispose() => _reader?.Dispose();
}
