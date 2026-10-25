using System.Diagnostics;
using System.IO;
using VoxDeck.App.Features.Library;
using VoxDeck.App.Features.Player;
using VoxDeck.App.Features.Toasts;
using VoxDeck.App.Hotkeys;
using VoxDeck.Core.Audio;
using VoxDeck.Core.Library;

namespace VoxDeck.App.Features.Editor;

// The slide-in panel for one sound: preview, name, volume, hotkey, start point, loop, favorite.
public sealed class SoundEditorViewModel : ViewModel
{
    const int DetailBars = 72;

    readonly LibraryViewModel _library;
    readonly PlayerViewModel _player;
    readonly HotkeyManager _hotkeys;
    readonly ToastsViewModel _toasts;

    public SoundEditorViewModel(LibraryViewModel library, PlayerViewModel player, HotkeyManager hotkeys, ToastsViewModel toasts)
    {
        _library = library;
        _player = player;
        _hotkeys = hotkeys;
        _toasts = toasts;
        player.Ticked += () =>
        {
            if (!IsOpen) return;
            Raise(nameof(Progress));
            Raise(nameof(TimeText));
            Raise(nameof(IsPlaying));
        };
        TogglePlay = new Command(() => { if (Item is not null) _player.Toggle(Item); });
        Seek = new Command(p => { if (Item is not null) _player.Seek(Item, (double)p!); });
        SetStart = new Command(p => SetStartAt(p is double fraction ? fraction * Duration : CurrentPosition));
        ResetStart = new Command(() => SetStartAt(0));
        CaptureHotkey = new Command(OnCaptureHotkey);
        ClearHotkey = new Command(() => { if (Item is not null) _hotkeys.AssignToSound(Item, null); Raise(""); });
        Remove = new Command(() =>
        {
            if (Item is not { } item) return;
            Close();
            _library.Remove.Execute(item);
        });
        ShowFile = new Command(() => { if (Item is not null) Process.Start("explorer.exe", $"/select,\"{Item.Model.Path}\""); });
        CloseCommand = new Command(Close);
    }

    public SoundItem? Item { get; private set; }
    public bool IsOpen { get; private set => Set(ref field, value); }
    public bool IsCapturing { get; private set => Set(ref field, value); }

    public string Name
    {
        get => Item?.Model.Title ?? "";
        set
        {
            var name = value.Trim();
            if (Item is null || name.Length == 0 || name == Item.Model.Title) return;
            Edit(Item.Model with { Title = name });
        }
    }

    public double Volume
    {
        get => Item?.Model.Volume ?? 1;
        set
        {
            if (Item is null) return;
            Edit(Item.Model with { Volume = value });
            if (Item.Playback is { Ended: false } p) p.Volume = (float)value;
        }
    }

    public bool Loop
    {
        get => Item?.Model.Loop ?? false;
        set
        {
            if (Item is null) return;
            Edit(Item.Model with { Loop = value });
            if (Item.Playback is { Ended: false } p) p.Loop = value;
        }
    }

    public bool Favorite
    {
        get => Item?.Model.Favorite ?? false;
        set
        {
            if (Item is not null) Edit(Item.Model with { Favorite = value });
        }
    }

    public bool IsPlaying => Item is { IsPlaying: true, IsPaused: false };
    public float[]? Peaks => Item?.DetailPeaks ?? Item?.TilePeaks;
    public double Progress => Item?.Progress ?? 0;
    public double StartMarker => Duration > 0 ? (Item?.Model.StartSeconds ?? 0) / Duration : 0;
    public bool HasStart => Item?.Model.StartSeconds > 0;
    public string StartText => HasStart ? $"Starts at {SoundItem.Time(Item!.Model.StartSeconds)}" : "Starts at the beginning";
    public string TimeText => Item is null ? "" : $"{SoundItem.Time(CurrentPosition ?? Item.Model.StartSeconds)} / {SoundItem.Time(Duration)}";
    public string? HotkeyText => Item?.HotkeyText;
    public string FileText => Item is null ? "" : Path.GetFileName(Item.Model.Path);
    public string FormatText => Item is null ? "" : $"{Item.Model.Format}  ·  {Item.DurationText}";

    public Command TogglePlay { get; }
    public Command Seek { get; }
    public Command SetStart { get; }
    public Command ResetStart { get; }
    public Command CaptureHotkey { get; }
    public Command ClearHotkey { get; }
    public Command Remove { get; }
    public Command ShowFile { get; }
    public Command CloseCommand { get; }

    double Duration => Item?.Playback?.Duration.TotalSeconds ?? Item?.Model.DurationSeconds ?? 0;
    double? CurrentPosition => Item?.Playback is { Ended: false } p ? p.Position.TotalSeconds : null;

    public void Open(SoundItem item)
    {
        if (IsCapturing) _hotkeys.CancelCapture();
        if (Item is not null) Item.IsSelected = false;
        Item = item;
        item.IsSelected = true;
        IsOpen = true;
        Raise("");
        LoadDetailPeaks(item);
    }

    public void Close()
    {
        if (IsCapturing) _hotkeys.CancelCapture();
        if (Item is not null) Item.IsSelected = false;
        IsOpen = false;
    }

    void Edit(Sound model)
    {
        _library.Edit(Item!, model);
        Raise("");
    }

    void SetStartAt(double? seconds)
    {
        if (Item is null) return;
        if (seconds is null)
        {
            _toasts.Info("Play the sound, then press “Use current position” at the moment it should begin.");
            return;
        }
        Edit(Item.Model with { StartSeconds = Math.Round(Math.Clamp(seconds.Value, 0, Math.Max(0, Duration - 0.05)), 2) });
    }

    void OnCaptureHotkey()
    {
        if (Item is not { } item) return;
        if (IsCapturing)
        {
            _hotkeys.CancelCapture();
            return;
        }
        IsCapturing = true;
        _hotkeys.Capture(
            hotkey =>
            {
                IsCapturing = false;
                _hotkeys.AssignToSound(item, hotkey);
                Raise("");
            },
            () => IsCapturing = false);
    }

    async void LoadDetailPeaks(SoundItem item)
    {
        if (item.DetailPeaks is not null) return;
        try
        {
            var (peaks, _) = await Task.Run(() => Waveform.Load(item.Model.Path, DetailBars));
            item.DetailPeaks = peaks;
            if (item == Item) Raise(nameof(Peaks));
        }
        catch (Exception)
        {
            // Missing or unreadable files show the tile waveform (or a flat line) and are reported when played.
        }
    }
}
