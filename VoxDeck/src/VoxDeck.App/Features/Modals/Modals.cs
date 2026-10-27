using System.ComponentModel;
using System.Windows.Data;
using VoxDeck.App.Features.Library;

namespace VoxDeck.App.Features.Modals;

// One dialog at a time over the whole window. Esc, Cancel or a click on the backdrop closes it.
public sealed class ModalHost : ViewModel
{
    public ModalHost() => Close = new Command(Hide);

    public ViewModel? Current { get; private set => Set(ref field, value); }
    public Command Close { get; }

    public void Show(ViewModel modal) => Current = modal;
    public void Hide() => Current = null;
}

/// <summary>A sound with a checkbox, for picking sounds while making a playlist.</summary>
public sealed class PickSound(SoundItem item, bool isChecked) : ViewModel
{
    public SoundItem Item => item;
    public bool IsChecked { get; set => Set(ref field, value); } = isChecked;
}

/// <summary>Name a playlist and, when creating one, pick its first sounds.</summary>
public sealed class PlaylistModal : ViewModel
{
    readonly ModalHost _host;
    readonly Action<string, string[]> _confirm;
    readonly ICollectionView _view;

    public PlaylistModal(ModalHost host, string title, string confirmText, string name, IEnumerable<PickSound> sounds, Action<string, string[]> confirm)
    {
        _host = host;
        _confirm = confirm;
        Title = title;
        ConfirmText = confirmText;
        Name = name;
        Sounds = [.. sounds];
        foreach (var sound in Sounds) sound.PropertyChanged += (_, _) => Raise(nameof(PickedText));
        _view = CollectionViewSource.GetDefaultView(Sounds);
        _view.Filter = o => Search.Length == 0 || ((PickSound)o).Item.Model.Title.Contains(Search, StringComparison.OrdinalIgnoreCase);
        Confirm = new Command(OnConfirm);
    }

    public string Title { get; }
    public string ConfirmText { get; }
    public List<PickSound> Sounds { get; }
    public bool HasSounds => Sounds.Count > 0;
    public Command Confirm { get; }
    public Command Cancel => _host.Close;

    public string Name
    {
        get;
        set
        {
            if (Set(ref field, value)) Raise(nameof(CanConfirm));
        }
    } = "";

    public bool CanConfirm => Name.Trim().Length > 0;

    public string Search
    {
        get;
        set
        {
            if (Set(ref field, value)) _view.Refresh();
        }
    } = "";

    public string PickedText => Sounds.Count(s => s.IsChecked) switch
    {
        0 => "Pick sounds now, or add them later",
        1 => "1 sound picked",
        var n => $"{n} sounds picked",
    };

    void OnConfirm()
    {
        if (!CanConfirm) return;
        _host.Hide();
        _confirm(Name.Trim(), [.. Sounds.Where(s => s.IsChecked).Select(s => s.Item.Model.Path)]);
    }
}

/// <summary>A playlist with a checkbox: ticking adds the sound, unticking removes it, right away.</summary>
public sealed class PlaylistToggle(FilterChip chip, bool isIn, Action<FilterChip, bool> changed) : ViewModel
{
    public FilterChip Chip => chip;

    public bool IsIn
    {
        get;
        set
        {
            if (Set(ref field, value)) changed(chip, value);
        }
    } = isIn;
}

/// <summary>"Add to playlist": every playlist with a checkbox, plus a way to make a new one.</summary>
public sealed class SaveToPlaylistModal(ModalHost host, SoundItem sound, IEnumerable<PlaylistToggle> playlists, Action newPlaylist) : ViewModel
{
    public string SoundTitle => sound.Model.Title;
    public List<PlaylistToggle> Playlists { get; } = [.. playlists];
    public bool HasPlaylists => Playlists.Count > 0;
    public Command NewPlaylist { get; } = new(newPlaylist);
    public Command Done => host.Close;
}

/// <summary>"Are you sure?" for things that are annoying to undo.</summary>
public sealed class ConfirmModal(ModalHost host, string title, string message, string confirmText, Action confirm) : ViewModel
{
    public string Title => title;
    public string Message => message;
    public string ConfirmText => confirmText;
    public Command Cancel => host.Close;

    public Command Confirm { get; } = new(() =>
    {
        host.Hide();
        confirm();
    });
}
