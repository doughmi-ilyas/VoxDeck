using System.Windows;
using System.Windows.Controls;
using VoxDeck.App.Controls;

namespace VoxDeck.App.Features.Editor;

public partial class SoundEditorView : UserControl
{
    public SoundEditorView() => InitializeComponent();

    /// <summary>Sections rise in one after another as the drawer slides open.</summary>
    public void AnimateIn()
    {
        var i = 0;
        foreach (UIElement child in Body.Children) Motion.Enter(child, fromY: 12, delay: 60 + i++ * 30);
    }
}
