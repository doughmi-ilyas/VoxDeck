namespace VoxDeck.App.Features.Palette;

public sealed record PaletteEntry(string Title, string Subtitle, string Icon, string? Shortcut, Action Run);

// Ctrl+K: type to find a sound or an action, Enter runs it.
public sealed class PaletteViewModel(Func<IEnumerable<PaletteEntry>> source) : ViewModel
{
    public bool IsOpen { get; private set => Set(ref field, value); }

    public string Query
    {
        get;
        set
        {
            if (Set(ref field, value)) Refresh();
        }
    } = "";

    public List<PaletteEntry> Results { get; private set => Set(ref field, value); } = [];
    public PaletteEntry? Selected { get; set => Set(ref field, value); }

    public void Open()
    {
        Query = "";
        Refresh();
        IsOpen = true;
    }

    public void Close() => IsOpen = false;

    public void RunSelected()
    {
        var entry = Selected;
        Close();
        entry?.Run();
    }

    public void Move(int delta)
    {
        if (Results.Count == 0) return;
        var index = Selected is null ? -1 : Results.IndexOf(Selected);
        Selected = Results[Math.Clamp(index + delta, 0, Results.Count - 1)];
    }

    void Refresh()
    {
        var words = Query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Results = source()
            .Where(e => words.All(w => e.Title.Contains(w, StringComparison.OrdinalIgnoreCase) || e.Subtitle.Contains(w, StringComparison.OrdinalIgnoreCase)))
            .Take(50)
            .ToList();
        Selected = Results.FirstOrDefault();
    }
}
