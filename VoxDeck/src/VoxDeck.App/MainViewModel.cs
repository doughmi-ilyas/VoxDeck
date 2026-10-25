using VoxDeck.App.Features.AudioSetup;
using VoxDeck.App.Features.Editor;
using VoxDeck.App.Features.Library;
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
    readonly SoundEngine? _engine;

    public MainViewModel()
    {
        Settings = new SettingsViewModel();
        Toasts = new ToastsViewModel();
        try
        {
            _engine = new SoundEngine();
        }
        catch (Exception ex)
        {
            Toasts.Error($"No sound output is available: {ex.Message}");
        }
        Player = new PlayerViewModel(_engine, Settings, Toasts);
        Library = new LibraryViewModel(Player, Toasts);
        Hotkeys = new HotkeyManager(Library, Settings, Player, Toasts);
        Settings.Hotkeys = Hotkeys;
        Library.Hotkeys = Hotkeys;
        Audio = new AudioSetupViewModel(Settings, _engine);
        Player.OutputName = Audio.OutputName;
        Editor = new SoundEditorViewModel(Library, Player, Hotkeys, Toasts);
        Library.EditRequested += Editor.Open;
        Palette = new PaletteViewModel(PaletteEntries);

        Pages = [Library, Audio, Settings];
        Page = Library;
        OpenPalette = new Command(Palette.Open);
        Import = new Command(() => Library.Import(LibraryViewModel.PickFiles()));
        OpenNowPlaying = new Command(() => { if (Player.NowPlaying is { } item) Editor.Open(item); });
        GoToAudio = new Command(() => Page = Audio);
    }

    public SettingsViewModel Settings { get; }
    public ToastsViewModel Toasts { get; }
    public PlayerViewModel Player { get; }
    public LibraryViewModel Library { get; }
    public HotkeyManager Hotkeys { get; }
    public AudioSetupViewModel Audio { get; }
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

    public Command OpenPalette { get; }
    public Command Import { get; }
    public Command OpenNowPlaying { get; }
    public Command GoToAudio { get; }

    IEnumerable<PaletteEntry> PaletteEntries()
    {
        var stopKey = Settings.StopAllHotkeyText;
        yield return new("Stop all sounds", "Action", "", stopKey, Player.StopEverything);
        yield return new("Add sounds…", "Action", "", "Ctrl+O", () => Import.Execute(null));
        yield return new(ThemeManager.IsDark ? "Switch to light theme" : "Switch to dark theme", "Action",
            ThemeManager.IsDark ? "" : "", null, () => Settings.Theme = ThemeManager.IsDark ? "Light" : "Dark");
        yield return new("Go to Library", "Navigate", "", null, () => Page = Library);
        yield return new("Go to Audio", "Navigate", "", null, () => Page = Audio);
        yield return new("Go to Settings", "Navigate", "", null, () => Page = Settings);
        foreach (var item in Library.Items)
            yield return new(item.Model.Title, $"Play sound  ·  {item.Model.Format}  ·  {item.DurationText}", "", item.HotkeyText, () => Player.Play(item));
    }

    public void Dispose()
    {
        Audio.Stop();
        Library.SaveNow();
        Settings.SaveNow();
        _engine?.Dispose();
    }
}
