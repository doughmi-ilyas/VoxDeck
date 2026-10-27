using VoxDeck.App.Features.AudioSetup;
using VoxDeck.App.Features.Library;
using VoxDeck.App.Features.Mixer;
using VoxDeck.App.Features.Settings;
using VoxDeck.Core.Audio;

namespace VoxDeck.App.Features.Onboarding;

public sealed record OnboardingStep(string Eyebrow, string Title, string Subtitle);

public sealed class ProgressSegment : ViewModel
{
    public bool Filled { get; set => Set(ref field, value); }
}

// First-run setup: splash, five short steps (mic, headphones, sounds, stop shortcut, voice chat), then "all set".
// Every step reuses the real controls behind it (Audio page, Mixer, Library, Settings), so choices apply right away.
public sealed class OnboardingViewModel : ViewModel
{
    public const int Splash = 0, Done = 6;

    readonly SettingsViewModel _settings;

    public OnboardingViewModel(SettingsViewModel settings, AudioSetupViewModel audio, MixerViewModel mixer, LibraryViewModel library)
    {
        _settings = settings;
        Audio = audio;
        Mixer = mixer;
        Library = library;
        IsOpen = !settings.Current.OnboardingDone;
        WantsVoiceChat = settings.Current.SoundRoute != SoundRoute.MeOnly && settings.Current.VoiceMixEnabled;
        Start = new Command(() => GoTo(1));
        Next = new Command(() => GoTo(Step + 1));
        Back = new Command(() => GoTo(Step - 1));
        Skip = new Command(Complete);
        Finish = new Command(Complete);
        Restart = new Command(() =>
        {
            Step = Splash;
            IsOpen = true;
            Opened?.Invoke();
        });
        ChooseVoiceChat = new Command(p => WantsVoiceChat = p is "yes");
        mixer.Router.Changed += RaiseVoiceState; // the cable can appear while this is open ("Check again")
    }

    void RaiseVoiceState()
    {
        Raise(nameof(NeedsCable));
        Raise(nameof(CableReady));
        Raise(nameof(VoiceChatText));
    }

    /// <summary>The view plays its exit animation, then the app slides in.</summary>
    public event Action? Finished;
    /// <summary>Opened again from Settings; the view replays the splash.</summary>
    public event Action? Opened;

    public AudioSetupViewModel Audio { get; }
    public MixerViewModel Mixer { get; }
    public LibraryViewModel Library { get; }
    public ShortcutSetting? StopAll => _settings.StopAllShortcut;

    public bool IsOpen { get; private set => Set(ref field, value); }

    public static readonly OnboardingStep[] Steps =
    [
        new("Microphone", "Which microphone is yours?", "VoxDeck uses the mic you already have. Say something and watch it react."),
        new("Headphones", "Where do you want to hear your sounds?", "Pick your headphones or speakers, then play a short test sound."),
        new("Sounds", "Add your first sounds", "MP3, WAV, M4A, AAC, WMA or FLAC. Your files stay exactly where they are."),
        new("Shortcut", "Stop everything with one key", "Pick a shortcut that works even inside a game. Press it any time to silence every sound."),
        new("Voice chat", "Should your friends hear your sounds?", "VoxDeck can mix your sounds into your mic for Discord and games."),
    ];

    public List<ProgressSegment> Segments { get; } = [.. Steps.Select(_ => new ProgressSegment())];

    /// <summary>0 = splash, 1..5 = steps, 6 = all set.</summary>
    public int Step
    {
        get;
        private set
        {
            if (!Set(ref field, value)) return;
            for (var i = 0; i < Segments.Count; i++) Segments[i].Filled = i < value;
            Raise(nameof(Current));
            Raise(nameof(CountText));
            Raise(nameof(NextText));
            Raise(nameof(InSteps));
        }
    }

    public OnboardingStep Current => Steps[Math.Clamp(Step - 1, 0, Steps.Length - 1)];
    public bool InSteps => Step is > Splash and < Done;
    public string CountText => $"{Step} / {Steps.Length}";
    public string NextText => Step == Steps.Length ? "Finish" : "Continue";

    public bool WantsVoiceChat
    {
        get;
        private set
        {
            if (!Set(ref field, value)) return;
            Raise(nameof(WantsNoVoiceChat));
            RaiseVoiceState();
        }
    }

    public bool WantsNoVoiceChat => !WantsVoiceChat;
    public bool NeedsCable => WantsVoiceChat && !Mixer.HasCable;
    public bool CableReady => WantsVoiceChat && Mixer.HasCable;

    // The "all set" summary.
    public string StopAllText => StopAll?.Text ?? "Not set";
    public string VoiceChatText => !WantsVoiceChat ? "Off" : Mixer.Router.IsRunning ? "On" : "Needs VB-CABLE";

    public Command Start { get; }
    public Command Next { get; }
    public Command Back { get; }
    public Command Skip { get; }
    public Command Finish { get; }
    public Command Restart { get; }
    public Command ChooseVoiceChat { get; }

    void GoTo(int step)
    {
        step = Math.Clamp(step, Splash, Done);
        if (Step == Steps.Length && step == Done) ApplyVoiceChoice();
        Step = step;
        Raise(nameof(StopAllText));
        RaiseVoiceState();
        // The mic meter (and Windows' "mic in use" light) only runs on the microphone step.
        if (step == 1) Audio.Start();
        else Audio.Stop();
    }

    void ApplyVoiceChoice()
    {
        var route = WantsVoiceChat ? SoundRoute.Both : SoundRoute.MeOnly;
        Mixer.Route = Mixer.RouteOptions.First(o => o.Route == route); // Both turns voice chat on when a cable is installed
    }

    void Complete()
    {
        Audio.Stop();
        StopAll?.CancelIfCapturing();
        _settings.Update(s => s with { OnboardingDone = true });
        Finished?.Invoke();
    }

    /// <summary>Called by the view once its exit animation is done.</summary>
    public void Close() => IsOpen = false;
}
