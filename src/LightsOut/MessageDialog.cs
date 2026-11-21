using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace LightsOut;

/// <summary>Minimal modal message box (Avalonia has no built-in one).</summary>
internal static class MessageDialog
{
    public static async Task Show(Window owner, string title, string text)
    {
        var window = new Window
        {
            Title = title,
            Width = 560,
            SizeToContent = SizeToContent.Height,
            MaxHeight = 560,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.Parse("#1F212A"))
        };

        var label = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.Parse("#C9CDDB")),
            Margin = new Thickness(20, 20, 20, 12)
        };

        var ok = new Button
        {
            Content = "OK",
            Width = 90,
            IsDefault = true,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(20, 0, 20, 20)
        };
        ok.Click += (_, _) => window.Close();

        var scroller = new ScrollViewer
        {
            Content = label,
            MaxHeight = 420,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        var panel = new DockPanel();
        DockPanel.SetDock(ok, Dock.Bottom);
        panel.Children.Add(ok);
        panel.Children.Add(scroller);
        window.Content = panel;

        window.Opened += (_, _) => ok.Focus();
        window.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Escape || e.Key == Avalonia.Input.Key.Enter)
            {
                window.Close();
                e.Handled = true;
            }
        };

        await window.ShowDialog<object?>(owner);
    }
}
