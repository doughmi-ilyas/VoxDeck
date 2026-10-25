namespace VoxDeck.Core.Audio;

// One press of a sound: the same audio playing on every active output — your headphones,
// plus the voice-chat cable while voice mixing is on. The first voice is the one you hear.
public sealed class Playback
{
    readonly Voice[] _voices;

    internal Playback(Voice[] voices) => _voices = voices;

    public TimeSpan Duration => _voices[0].Duration;
    public bool Ended => _voices.All(v => v.Ended);

    public TimeSpan Position
    {
        get => _voices[0].Position;
        set
        {
            foreach (var voice in _voices) voice.Position = value;
        }
    }

    public bool Paused
    {
        get => _voices[0].Paused;
        set
        {
            foreach (var voice in _voices) voice.Paused = value;
        }
    }

    public bool Loop
    {
        get => _voices[0].Loop;
        set
        {
            foreach (var voice in _voices) voice.Loop = value;
        }
    }

    public float Volume
    {
        get => _voices[0].Volume;
        set
        {
            foreach (var voice in _voices) voice.Volume = value;
        }
    }

    public void Stop()
    {
        foreach (var voice in _voices) voice.Stop();
    }
}
