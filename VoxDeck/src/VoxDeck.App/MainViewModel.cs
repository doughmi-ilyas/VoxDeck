using VoxDeck.App.Features.AudioSetup;
using VoxDeck.App.Features.Editor;
using VoxDeck.App.Features.Library;
using VoxDeck.App.Features.Mixer;
using VoxDeck.App.Features.Modals;
using VoxDeck.App.Features.Onboarding;
using VoxDeck.App.Features.Palette;
using VoxDeck.App.Features.Player;
using VoxDeck.App.Features.Settings;
using VoxDeck.App.Features.Toasts;
using VoxDeck.App.Hotkeys;
using VoxDeck.App.Theme;
using VoxDeck.Core.Audio;

namespace VoxDeck.App;

public sealed class MainViewModel : ViewModel, IDisposable
{
    readonly SoundEngine _engine = new();

    public MainViewModel()
    {
        Settings = new SettingsViewModel();
        Toasts = new ToastsViewModel();
        Router = new AudioRouter(_engine, Settings, Toasts);
        Player = new PlayerViewModel(_engine, Settings, Toasts);
        Library = new LibraryViewModel(Player, Toasts, Modals);
        Mixer = new MixerViewModel(_engine, Router, Settings, Player, Toasts);
        Mixer.ShowSetup = () => Page = Mixer;
        Hotkeys = new HotkeyManager(Library, Settings, Player, Mixer, Toasts);
        Settings.AttachHotkeys(Hotkeys);
        Library.Hotkeys = Hotkeys;
        Audio = new AudioSetupViewModel(Settings, _engine, Router);
        Onboarding = new OnboardingViewModel(Settings, Audio, Mixer, Library);
        Editor = new SoundEditorViewModel(Library, Player, Hotkeys, Toasts);
        Library.EditRequested += Editor.Open;
        Palette = new PaletteViewModel(PaletteEntries);
        Router.Refresh(); // opens the output devices

        Pages = [Library, Mixer, Audio, Settings];
        Page = Library;
        OpenPalette = new Command(Palette.Open);
        Import = new Command(() => Library.Import(LibraryViewModel.PickFiles()));
        OpenNowPlaying = new Command(() => { if (Player.NowPlaying is { } item) Editor.Open(item); });
        GoToAudio = new Command(() => Page = Audio);
        GoToMixer = new Command(() => Page = Mixer);
    }

    public SettingsViewModel Settings { get; }
    public ToastsViewModel Toasts { get; }
    public AudioRouter Router { get; }
    public PlayerViewModel Player { get; }
    public LibraryViewModel Library { get; }
    public MixerViewModel Mixer { get; }
    public HotkeyManager Hotkeys { get; }
    public AudioSetupViewModel Audio { get; }
    public OnboardingViewModel Onboarding { get; }
    public SoundEditorViewModel Editor { get; }
    public PaletteViewModel Palette { get; }

    // Sidebar entries; a new page is one more item here plus a DataTemplate in MainWindow.xaml.
    public ViewModel[] Pages { get; }

    public ViewModel Page
    {
        get;
        set
        {
            if (Set(ref field, value)) Editor?.Close();
        }
    }

    public bool IsDraggingFiles { get; set => Set(ref field, value); }
    /// <summary>The window is wide enough for the full sidebar (set by the window as it resizes).</summary>
    public bool IsWide { get; set => Set(ref field, value); } = true;
    public ModalHost Modals { get; } = new();

    public Command OpenPalette { get; }
    public Command Import { get; }
    public Command OpenNowPlaying { get; }
    public Command GoToAudio { get; }
    public Command GoToMixer { get; }

    IEnumerable<PaletteEntry> PaletteEntries()
    {
        yield return new("Stop all sounds", "Action", "", Settings.StopAllShortcut?.Text, Player.StopEverything);
        yield return new("Add sounds…", "Action", "", "Ctrl+O", () => Import.Execute(null));
        if (Router.Cables.Count > 0)
            yield return new(Mixer.Enabled ? "Stop sending to voice chat" : "Send to voice chat", "Voice mixing", "", null, () => Mixer.Enabled = !Mixer.Enabled);
        foreach (var chip in Library.Filters.Where(f => f.IsPlaylist))
            yield return new($"Play playlist: {chip.Label}", $"Playlist  ·  {chip.Count} sounds", "", null, () => Library.PlayAll(chip, shuffle: false));
        foreach (var option in Mixer.RouteOptions.Where(o => o != Mixer.Route))
            yield return new($"Sounds play for: {option.Label}", "Who hears your sounds", option.Icon, null, () => Mixer.Route = option);
        if (Router.IsRunning)
            yield return new(Mixer.MicMuted ? "Unmute microphone" : "Mute microphone", "Voice mixing", "", Settings.MuteMicShortcut?.Text, () => Mixer.MicMuted = !Mixer.MicMuted);
        yield return new(ThemeManager.IsDark ? "Switch to light theme" : "Switch to dark theme", "Action",
            ThemeManager.IsDark ? "" : "", null, () => Settings.Theme = ThemeManager.IsDark ? "Light" : "Dark");
        foreach (var page in Pages)
            yield return new($"Go to {((IPage)page).Title}", "Navigate", ((IPage)page).Icon, null, () => Page = page);
        foreach (var item in Library.Items)
            yield return new(item.Model.Title, $"Play sound  ·  {item.Model.Format}  ·  {item.DurationText}", "", item.HotkeyText, () => Player.Play(item));
    }

    public void Dispose()
    {
        Audio.Stop();
        Library.SaveNow();
        Settings.SaveNow();
        Router.Dispose();
        _engine.Dispose();
    }
}
