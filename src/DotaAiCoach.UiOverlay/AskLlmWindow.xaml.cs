using System.Windows;
using DotaAiCoach.Host;
using DotaAiCoach.UiOverlay.ViewModels;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace DotaAiCoach.UiOverlay;

/// <summary>
/// The manual "ask for a build" window — user uploads (or drags in) a
/// screenshot of their own scoreboard, which is auto-extracted (enemy
/// hero level/KDA), then gets a one-off LLM recommendation. Mirrors
/// dota-plus-free's TickForm.tsx drag-and-drop-then-auto-extract flow. A
/// separate window rather than overlay panel space, mirroring
/// ProfileWindow's precedent.
/// </summary>
public partial class AskLlmWindow : Window
{
    private readonly AskLlmViewModel _viewModel;

    public AskLlmWindow(ILlmCoachSession session)
    {
        InitializeComponent();
        _viewModel = new AskLlmViewModel(session, Dispatcher);
        DataContext = _viewModel;
    }

    private async void OnAskClick(object sender, RoutedEventArgs e)
    {
        await _viewModel.AskAsync(CancellationToken.None);
    }

    private async void OnPickImageClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif",
            Title = "Selecciona la captura del scoreboard",
        };

        if (dialog.ShowDialog(this) == true)
        {
            await _viewModel.ExtractScoreboardAsync(dialog.FileName, CancellationToken.None);
        }
    }

    private void OnWindowDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnWindowDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
        {
            await _viewModel.ExtractScoreboardAsync(files[0], CancellationToken.None);
        }
    }
}
