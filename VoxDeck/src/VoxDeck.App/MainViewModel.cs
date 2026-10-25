using VoxDeck.App.Features.AudioSetup;
using VoxDeck.App.Features.Library;
using VoxDeck.App.Features.Player;
using VoxDeck.Core.Audio;

namespace VoxDeck.App;

public sealed class MainViewModel : ViewModel, IDisposable
{
    readonly SoundEngine? _engine;
    readonly LibraryViewModel _library;
    readonly AudioSetupViewModel _audioSetup = new();

    public MainViewModel()
    {
        string? error = null;
        try
        {
            _engine = new SoundEngine();
        }
        catch (Exception ex)
        {
            error = $"No sound output available: {ex.Message}";
        }
        Player = new PlayerViewModel(_engine, error);
        _library = new LibraryViewModel(Player);
        Pages = [_library, _audioSetup];
        Page = _library;
        StopAll = new Command(() => _engine?.StopAll());
    }

    // Sidebar entries; a new page is one more item here plus a DataTemplate in MainWindow.xaml.
    public ViewModel[] Pages { get; }
    public ViewModel Page { get; set => Set(ref field, value); }
    public PlayerViewModel Player { get; }

    public double MasterVolume
    {
        get;
        set
        {
            if (Set(ref field, value) && _engine is not null) _engine.MasterVolume = (float)value;
        }
    } = 1;

    public Command StopAll { get; }

    public void Dispose()
    {
        _audioSetup.Stop();
        _library.SaveNow();
        _engine?.Dispose();
    }
}
