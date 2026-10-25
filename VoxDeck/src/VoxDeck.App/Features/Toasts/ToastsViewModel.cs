using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace VoxDeck.App.Features.Toasts;

/// <param name="Kind">"info", "ok" or "error".</param>
public sealed record Toast(string Text, string Icon, string Kind, string? ActionText = null, Action? Action = null);

// Short messages at the bottom of the window. Errors and confirmations go here instead of inline text.
public sealed class ToastsViewModel
{
    public ToastsViewModel() => RunAction = new Command(p =>
    {
        var toast = (Toast)p!;
        Items.Remove(toast);
        toast.Action?.Invoke();
    });

    public ObservableCollection<Toast> Items { get; } = [];
    public Command RunAction { get; }

    public void Info(string text) => Show(new Toast(text, "", "info"));
    public void Success(string text) => Show(new Toast(text, "", "ok"));
    public void Error(string text) => Show(new Toast(text, "", "error"));

    public void Show(Toast toast)
    {
        Items.Add(toast);
        while (Items.Count > 3) Items.RemoveAt(0);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(toast.Action is null ? 4 : 7) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Items.Remove(toast);
        };
        timer.Start();
    }
}
