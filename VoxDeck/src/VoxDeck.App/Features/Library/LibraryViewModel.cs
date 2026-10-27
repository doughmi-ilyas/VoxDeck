using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using System.Windows.Threading;
using Microsoft.Win32;
using VoxDeck.App.Features.Modals;
using VoxDeck.App.Features.Player;
using VoxDeck.App.Features.Toasts;
using VoxDeck.App.Hotkeys;
using VoxDeck.Core.Audio;
using VoxDeck.Core.Library;

namespace VoxDeck.App.Features.Library;

// A filter above the tiles: All, Favorites, Recent, or one of your playlists.
public sealed class FilterChip(string label, Playlist? playlist = null) : ViewModel
{
    public string Label => Playlist?.Name ?? label;
    public bool IsPlaylist => Playlist is not null;
    public int Count { get; set => Set(ref field, value); }

    public Playlist? Playlist
    {
        get;
        set
        {
            if (Set(ref field, value)) Raise(nameof(Label));
        }
    } = playlist;

    /// <summary>A dragged sound is over this chip.</summary>
    public bool IsDropTarget { get; set => Set(ref field, value); }

    public override string ToString() => Label; // what screen readers announce
}

public sealed class LibraryViewModel : ViewModel, IPage
{
    const int TileBars = 32;

    readonly PlayerViewModel _player;
    readonly ToastsViewModel _toasts;
    readonly ModalHost _modals;
    readonly ListCollectionView _view;
    // Slider drags edit settings many times a second; write the file once they settle.
    readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    bool _computingPeaks;

    public LibraryViewModel(PlayerViewModel player, ToastsViewModel toasts, ModalHost modals)
    {
        _player = player;
        _toasts = toasts;
        _modals = modals;
        Filters = [new FilterChip("All"), new FilterChip("Favorites"), new FilterChip("Recent"),
            .. Playlist.LoadAll(Playlist.DefaultPath).Select(CreatePlaylistChip)];
        Filter = Filters[0];
        Items = new(SoundLibrary.Load(SoundLibrary.DefaultPath).Select(Create));
        _view = (ListCollectionView)CollectionViewSource.GetDefaultView(Items);
        _view.Filter = Matches;
        Items.CollectionChanged += (_, _) => UpdateCounts();
        _saveTimer.Tick += (_, _) => SaveNow();
        player.Started += item => Edit(item, item.Model with
        {
            LastPlayed = DateTime.Now,
            DurationSeconds = item.Playback?.Duration.TotalSeconds ?? item.Model.DurationSeconds,
        });

        Play = new Command(p => OnPlay((SoundItem)p!));
        ToggleFavorite = new Command(p => OnToggleFavorite((SoundItem)p!));
        EditSound = new Command(p => EditRequested?.Invoke((SoundItem)p!));
        Remove = new Command(p => RemoveItem((SoundItem)p!));
        ImportFiles = new Command(() => Import(PickFiles()));
        ClearSearch = new Command(() => Search = "");
        ResetVolumes = new Command(OnResetVolumes);
        NewPlaylist = new Command(() => OpenNewPlaylist());
        AddToPlaylist = new Command(p => OpenSaveToPlaylist((SoundItem)p!));
        PlayPlaylist = new Command(() => PlayAll(Filter, shuffle: false));
        ShufflePlaylist = new Command(() => PlayAll(Filter, shuffle: true));
        RenamePlaylist = new Command(OpenRename);
        DeletePlaylist = new Command(ConfirmDeletePlaylist);
        RemoveFromPlaylist = new Command(p => OnRemoveFromPlaylist((SoundItem)p!));
        MoveEarlier = new Command(p => MoveInPlaylist((SoundItem)p!, -1));
        MoveLater = new Command(p => MoveInPlaylist((SoundItem)p!, +1));
        ShowAll = new Command(() => Filter = Filters[0]);
        UpdateCounts();
        ComputeMissingPeaks();
    }

    public event Action<SoundItem>? EditRequested;

    public string Title => "Library";
    public string Icon => "";
    public string Badge => Items.Count.ToString();

    public HotkeyManager? Hotkeys { get; set; }
    public ObservableCollection<SoundItem> Items { get; }
    public ObservableCollection<FilterChip> Filters { get; }

    public FilterChip Filter
    {
        get;
        set
        {
            if (value is null || !Set(ref field, value)) return;
            Refresh();
            Raise(nameof(IsPlaylistView));
            Raise(nameof(DragHint));
            Raise(nameof(PageTitle));
            Raise(nameof(Subtitle));
            Raise(nameof(IsPlaylistEmpty));
        }
    }

    /// <summary>A sound is being dragged: playlists light up as drop targets and a hint shows.</summary>
    public bool IsDragging { get; set => Set(ref field, value); }

    public bool IsPlaylistView => Filter.IsPlaylist;
    public bool HasPlaylists => Filters.Any(f => f.IsPlaylist);
    public bool IsPlaylistEmpty => Filter.IsPlaylist && Filter.Count == 0;
    public string PageTitle => Filter.Playlist?.Name ?? "Library";

    public string DragHint => IsPlaylistView
        ? "Drop on a sound to move it there, or on another playlist to add it"
        : "Drop on a playlist to add it, or on “New playlist” to start one";

    public string Search
    {
        get;
        set
        {
            if (Set(ref field, value)) Refresh();
        }
    } = "";

    public string Subtitle
    {
        get
        {
            if (Filter.IsPlaylist)
            {
                var sounds = PlaylistItems(Filter);
                var total = sounds.Sum(i => i.Model.DurationSeconds ?? 0);
                return sounds.Count == 0 ? "Playlist  ·  empty"
                    : $"Playlist  ·  {(sounds.Count == 1 ? "1 sound" : $"{sounds.Count} sounds")}  ·  {SoundItem.Time(total)}  ·  plays in this order";
            }
            var parts = new List<string> { Items.Count == 1 ? "1 sound" : $"{Items.Count} sounds" };
            if (Filters[1].Count > 0) parts.Add($"{Filters[1].Count} favorite{(Filters[1].Count == 1 ? "" : "s")}");
            var withKeys = Items.Count(i => i.Model.Hotkey is not null);
            if (withKeys > 0) parts.Add($"{withKeys} with hotkeys");
            return string.Join("  ·  ", parts);
        }
    }

    public bool IsEmpty => Items.Count == 0;
    public bool NoResults => Items.Count > 0 && _view.IsEmpty && !IsPlaylistEmpty;

    public int CustomVolumeCount => Items.Count(i => i.Model.Volume != 1);
    public string CustomVolumeText => CustomVolumeCount == 1 ? "1 sound has its own volume" : $"{CustomVolumeCount} sounds have their own volume";

    public Command Play { get; }
    public Command ToggleFavorite { get; }
    public Command EditSound { get; }
    public Command Remove { get; }
    public Command ImportFiles { get; }
    public Command ClearSearch { get; }
    public Command ResetVolumes { get; }
    public Command NewPlaylist { get; }
    public Command AddToPlaylist { get; }
    public Command PlayPlaylist { get; }
    public Command ShufflePlaylist { get; }
    public Command RenamePlaylist { get; }
    public Command DeletePlaylist { get; }
    public Command RemoveFromPlaylist { get; }
    public Command MoveEarlier { get; }
    public Command MoveLater { get; }
    public Command ShowAll { get; }

    public static string[] PickFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Add sounds",
            Multiselect = true,
            Filter = $"Audio files|{string.Join(";", SoundLibrary.Extensions.Select(x => "*" + x))}",
        };
        return dialog.ShowDialog() == true ? dialog.FileNames : [];
    }

    public void Import(IReadOnlyCollection<string> paths)
    {
        if (paths.Count == 0) return;
        var added = 0;
        var skipped = 0;
        var supported = new List<string>();
        foreach (var path in paths)
        {
            if (!SoundLibrary.IsSupported(path))
            {
                skipped++;
                continue;
            }
            supported.Add(path);
            if (!Items.Any(i => string.Equals(i.Model.Path, path, StringComparison.OrdinalIgnoreCase)))
            {
                Items.Add(Create(new Sound(Path.GetFileNameWithoutExtension(path), path)));
                added++;
            }
        }
        SaveNow();
        // Adding sounds while a playlist is open puts them in that playlist too (even ones already in the library).
        if (Filter.Playlist is { } playlist && supported.Count > 0)
        {
            var before = playlist.Paths.Length;
            UpdatePlaylist(Filter, supported.Aggregate(playlist, (p, path) => p.Add(path)));
            var count = Filter.Playlist!.Paths.Length - before;
            _toasts.Success(count == 0 ? $"Those sounds are already in “{Filter.Label}”."
                : $"Added {(count == 1 ? "1 sound" : $"{count} sounds")} to “{Filter.Label}”.");
        }
        else if (added > 0) _toasts.Success(added == 1 ? "Added 1 sound." : $"Added {added} sounds.");
        else if (skipped == 0) _toasts.Info("Those sounds are already in your library.");
        if (skipped > 0)
            _toasts.Error($"Skipped {skipped} file{(skipped == 1 ? "" : "s")}. VoxDeck plays {string.Join(", ", SoundLibrary.Extensions)}.");
        ComputeMissingPeaks();
    }

    public void Edit(SoundItem item, Sound model)
    {
        item.Model = model;
        _saveTimer.Stop();
        _saveTimer.Start();
        UpdateCounts();
    }

    public void SaveNow()
    {
        _saveTimer.Stop();
        SoundLibrary.Save(SoundLibrary.DefaultPath, Items.Select(i => i.Model));
    }

    SoundItem Create(Sound sound) => new(sound, (item, fraction) => _player.Seek(item, fraction));

    bool Matches(object o)
    {
        var sound = ((SoundItem)o).Model;
        if (Filter == Filters[1] && !sound.Favorite) return false;
        if (Filter == Filters[2] && sound.LastPlayed is null) return false;
        if (Filter.Playlist is { } playlist && !playlist.Contains(sound.Path)) return false;
        return Search.Length == 0 || sound.Title.Contains(Search, StringComparison.OrdinalIgnoreCase);
    }

    void Refresh()
    {
        if (_view is null) return; // the first Filter assignment happens before the view exists
        using (_view.DeferRefresh())
        {
            _view.CustomSort = null;
            _view.SortDescriptions.Clear();
            if (Filter == Filters[2]) _view.SortDescriptions.Add(new SortDescription("Model.LastPlayed", ListSortDirection.Descending));
            if (Filter.Playlist is { } playlist)
            {
                var order = playlist.Paths.Select((path, i) => (path, i)).DistinctBy(p => p.path, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(p => p.path, p => p.i, StringComparer.OrdinalIgnoreCase);
                _view.CustomSort = Comparer<object>.Create((a, b) => order[((SoundItem)a).Model.Path].CompareTo(order[((SoundItem)b).Model.Path]));
            }
        }
        Raise(nameof(NoResults));
    }

    void UpdateCounts()
    {
        Filters[0].Count = Items.Count;
        Filters[1].Count = Items.Count(i => i.Model.Favorite);
        Filters[2].Count = Items.Count(i => i.Model.LastPlayed is not null);
        foreach (var chip in Filters.Where(f => f.IsPlaylist)) chip.Count = PlaylistItems(chip).Count;
        Raise(nameof(IsPlaylistEmpty));
        Raise(nameof(HasPlaylists));
        Raise(nameof(Subtitle));
        Raise(nameof(Badge));
        Raise(nameof(IsEmpty));
        Raise(nameof(NoResults));
        Raise(nameof(CustomVolumeCount));
        Raise(nameof(CustomVolumeText));
    }

    void OnResetVolumes()
    {
        var before = Items.Where(i => i.Model.Volume != 1).Select(i => (Item: i, i.Model.Volume)).ToList();
        foreach (var (item, _) in before) SetVolume(item, 1);
        _toasts.Show(new Toast("Every sound now plays at the same volume.", "", "ok", "Undo",
            () => before.ForEach(b => SetVolume(b.Item, b.Volume))));
    }

    void SetVolume(SoundItem item, double volume)
    {
        Edit(item, item.Model with { Volume = volume });
        if (item.Playback is { Ended: false } p) p.Volume = (float)volume;
    }

    // ---- Playlists ----

    /// <summary>Plays a playlist from the top, or shuffled. Also used by the command palette.</summary>
    public void PlayAll(FilterChip chip, bool shuffle)
    {
        if (chip.Playlist is not { } playlist) return;
        var sounds = PlaylistItems(chip);
        if (sounds.Count == 0) _toasts.Info("This playlist is empty. Right-click a sound and choose “Add to playlist”.");
        else _player.PlayAll(sounds, playlist.Name, shuffle: shuffle);
    }

    // Inside a playlist, pressing a sound plays the playlist from that sound on.
    void OnPlay(SoundItem item)
    {
        if (Filter.Playlist is { } playlist) _player.PlayAll(PlaylistItems(Filter), playlist.Name, item);
        else _player.Play(item);
    }

    List<SoundItem> PlaylistItems(FilterChip chip) =>
        [.. chip.Playlist!.Paths.Select(path => Items.FirstOrDefault(i => string.Equals(i.Model.Path, path, StringComparison.OrdinalIgnoreCase))).OfType<SoundItem>()];

    static FilterChip CreatePlaylistChip(Playlist playlist) => new("", playlist);

    /// <summary>The "New playlist" dialog: a name plus every sound to pick from (<paramref name="with"/> starts ticked).</summary>
    void OpenNewPlaylist(SoundItem? with = null)
    {
        var name = "Playlist 1";
        for (var n = 2; Filters.Any(f => f.Playlist?.Name == name); n++) name = $"Playlist {n}";
        var sounds = Items.OrderByDescending(i => i == with).Select(i => new PickSound(i, i == with));
        _modals.Show(new PlaylistModal(_modals, "New playlist", "Create", name, sounds, (chosen, paths) =>
        {
            var chip = CreatePlaylistChip(new Playlist(chosen, paths));
            Filters.Add(chip);
            SavePlaylists();
            UpdateCounts();
            Filter = chip;
            _toasts.Success(paths.Length == 0
                ? $"Made “{chosen}”. Drag sounds onto it, or use ➕ on a sound."
                : $"Made “{chosen}” with {(paths.Length == 1 ? "1 sound" : $"{paths.Length} sounds")}.");
        }));
    }

    /// <summary>The "Add to playlist" dialog: tick or untick playlists for one sound.</summary>
    void OpenSaveToPlaylist(SoundItem item)
    {
        var playlists = Filters.Where(f => f.IsPlaylist).Select(chip => new PlaylistToggle(chip, chip.Playlist!.Contains(item.Model.Path),
            (c, add) => UpdatePlaylist(c, add ? c.Playlist!.Add(item.Model.Path) : c.Playlist!.Remove(item.Model.Path))));
        _modals.Show(new SaveToPlaylistModal(_modals, item, playlists, () => OpenNewPlaylist(item)));
    }

    void OpenRename()
    {
        if (Filter is not { Playlist: { } playlist } chip) return;
        _modals.Show(new PlaylistModal(_modals, "Rename playlist", "Save", playlist.Name, [],
            (name, _) => UpdatePlaylist(chip, chip.Playlist! with { Name = name })));
    }

    // ---- Drag and drop (started by LibraryView) ----

    /// <summary>A sound dropped on a playlist chip.</summary>
    public void DropOnPlaylist(FilterChip chip, SoundItem item)
    {
        if (chip.IsPlaylist) AddToPlaylistAndTell(chip, item);
    }

    /// <summary>A sound dropped on "New playlist": the dialog opens with it ticked.</summary>
    public void DropOnNewPlaylist(SoundItem item) => OpenNewPlaylist(item);

    /// <summary>Inside a playlist, a sound dropped on another one moves just before it.</summary>
    public void DropBefore(SoundItem dragged, SoundItem target)
    {
        if (Filter.Playlist is { } playlist) UpdatePlaylist(Filter, playlist.MoveBefore(dragged.Model.Path, target.Model.Path));
    }

    void UpdatePlaylist(FilterChip chip, Playlist playlist)
    {
        chip.Playlist = playlist;
        SavePlaylists();
        UpdateCounts();
        if (chip != Filter) return;
        Refresh();
        Raise(nameof(PageTitle));
    }

    void SavePlaylists() => Playlist.SaveAll(Playlist.DefaultPath, Filters.Select(f => f.Playlist).OfType<Playlist>());

    void AddToPlaylistAndTell(FilterChip chip, SoundItem item)
    {
        if (chip.Playlist!.Contains(item.Model.Path))
        {
            _toasts.Info($"“{item.Model.Title}” is already in “{chip.Label}”.");
            return;
        }
        UpdatePlaylist(chip, chip.Playlist.Add(item.Model.Path));
        _toasts.Show(new Toast($"Added “{item.Model.Title}” to “{chip.Label}”.", "", "ok", "Open", () => Filter = chip));
    }

    void OnRemoveFromPlaylist(SoundItem item)
    {
        if (Filter.Playlist is not { } before) return;
        var chip = Filter;
        UpdatePlaylist(chip, before.Remove(item.Model.Path));
        _toasts.Show(new Toast($"Removed “{item.Model.Title}” from “{chip.Label}”.", "", "info", "Undo", () => UpdatePlaylist(chip, before)));
    }

    void MoveInPlaylist(SoundItem item, int step)
    {
        if (Filter.Playlist is { } playlist) UpdatePlaylist(Filter, playlist.Move(item.Model.Path, step));
    }

    void ConfirmDeletePlaylist()
    {
        if (Filter is not { Playlist: not null } chip) return;
        _modals.Show(new ConfirmModal(_modals, $"Delete “{chip.Label}”?",
            "The playlist goes away. Its sounds stay in your library.", "Delete playlist", () => DeletePlaylistNow(chip)));
    }

    void DeletePlaylistNow(FilterChip chip)
    {
        var index = Filters.IndexOf(chip);
        if (Filter == chip) Filter = Filters[0];
        Filters.Remove(chip);
        SavePlaylists();
        UpdateCounts();
        _toasts.Show(new Toast($"Deleted “{chip.Label}”. Its sounds are still in your library.", "", "info", "Undo", () =>
        {
            Filters.Insert(Math.Min(index, Filters.Count), chip);
            SavePlaylists();
            UpdateCounts();
            Filter = chip;
        }));
    }

    void OnToggleFavorite(SoundItem item)
    {
        Edit(item, item.Model with { Favorite = !item.Model.Favorite });
        if (Filter == Filters[1]) Refresh();
    }

    void RemoveItem(SoundItem item)
    {
        var index = Items.IndexOf(item);
        Items.Remove(item);
        SaveNow();
        Hotkeys?.Refresh();
        _toasts.Show(new Toast($"Removed “{item.Model.Title}”. The file is still on your PC.", "", "info", "Undo", () =>
        {
            Items.Insert(Math.Min(index, Items.Count), item);
            SaveNow();
            Hotkeys?.Refresh();
        }));
    }

    // Tile waveforms are computed once in the background and saved with the library.
    async void ComputeMissingPeaks()
    {
        if (_computingPeaks) return;
        _computingPeaks = true;
        try
        {
            while (Items.FirstOrDefault(i => i.Model.Peaks is null && !i.PeaksFailed) is { } item)
            {
                try
                {
                    var (peaks, duration) = await Task.Run(() => Waveform.Load(item.Model.Path, TileBars));
                    Edit(item, item.Model with
                    {
                        Peaks = [.. peaks.Select(p => (byte)Math.Round(p * 255))],
                        DurationSeconds = duration.TotalSeconds,
                    });
                }
                catch (Exception)
                {
                    item.PeaksFailed = true;
                    item.IsMissing = !File.Exists(item.Model.Path);
                }
            }
        }
        finally
        {
            _computingPeaks = false;
        }
    }
}
