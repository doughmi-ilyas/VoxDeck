namespace VoxDeck.Core.Audio;

/// <summary>Who hears a sound. Stored as a number in the library, so keep the values stable.</summary>
public enum SoundRoute
{
    /// <summary>Your headphones and the voice chat.</summary>
    Both = 0,
    /// <summary>Only your headphones; the voice chat doesn't hear it.</summary>
    MeOnly = 1,
    /// <summary>Only the voice chat; you don't hear it. Needs voice mixing on.</summary>
    CallOnly = 2,
}

public static class SoundRoutes
{
    /// <summary>The outputs a sound plays on, first one first. Either output may be closed (null).</summary>
    public static T[] Pick<T>(SoundRoute route, T? monitor, T? call) where T : class => route switch
    {
        SoundRoute.MeOnly => monitor is null ? [] : [monitor],
        SoundRoute.CallOnly => call is null ? [] : [call],
        _ => new[] { monitor, call }.OfType<T>().ToArray(),
    };
}
