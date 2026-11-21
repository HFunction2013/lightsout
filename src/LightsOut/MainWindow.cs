using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace LightsOut;

internal sealed class MainWindow : Window
{
    private const int MinSide = 2;
    private const int MaxSide = 40;

    private readonly BoardView _board = new();
    private readonly TextBlock _statusLeft = new();
    private readonly TextBlock _statusRight = new();

    private int _rows = 5;
    private int _cols = 5;
    private byte[] _cells = Array.Empty<byte>();
    private int _moves;
    private bool _solved;
    private bool _edit;
    private byte[]? _hint;
    private int _hintCount;

    private static readonly IBrush BgBrush = new SolidColorBrush(Color.Parse("#16171E"));
    private static readonly IBrush StatusBrush = new SolidColorBrush(Color.Parse("#1F212A"));
    private static readonly IBrush TextBrush = new SolidColorBrush(Color.Parse("#B2B6C6"));
    private static readonly IBrush MutedBrush = new SolidColorBrush(Color.Parse("#787C8C"));
    private static readonly IBrush WinBrush = new SolidColorBrush(Color.Parse("#80DB5C"));
    private static readonly IBrush HintBrush = new SolidColorBrush(Color.Parse("#60DEBA"));

    public MainWindow()
    {
        Title = "Lights Out";
        Background = BgBrush;
        Width = 470;
        Height = 590;
        MinWidth = 300;
        MinHeight = 380;
        Content = BuildContent();
        _board.CellPressed += OnCellPressed;
        KeyDown += OnKeyDown;
        NewGame(5, 5);
    }

    /* ------------------------------------------------------------ layout */

    private Control BuildContent()
    {
        var menu = BuildMenu();
        DockPanel.SetDock(menu, Dock.Top);

        var status = new Grid
        {
            Background = StatusBrush,
            Height = 38,
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };
        DockPanel.SetDock(status, Dock.Bottom);

        _statusLeft.VerticalAlignment = VerticalAlignment.Center;
        _statusLeft.Margin = new Thickness(16, 0, 8, 0);
        _statusLeft.FontSize = 12;
        _statusLeft.Foreground = TextBrush;

        _statusRight.VerticalAlignment = VerticalAlignment.Center;
        _statusRight.Margin = new Thickness(8, 0, 16, 0);
        _statusRight.FontSize = 12;
        _statusRight.Foreground = MutedBrush;

        status.Children.Add(_statusLeft);
        status.Children.Add(_statusRight);
        Grid.SetColumn(_statusRight, 1);

        var dock = new DockPanel();
        dock.Children.Add(menu);
        dock.Children.Add(status);
        dock.Children.Add(_board);
        return dock;
    }

    private Menu BuildMenu()
    {
        var menu = new Menu();

        var game = new MenuItem { Header = "Game" };
        var newGame = new MenuItem { Header = "New game" };
        foreach (int n in new[] { 3, 4, 5, 6, 7, 8, 9, 10, 12, 15, 20, 25 })
        {
            var item = new MenuItem { Header = $"{n}x{n}" };
            item.Click += (_, _) => NewGame(n, n);
            newGame.Items.Add(item);
        }
        game.Items.Add(newGame);

        var custom = new MenuItem { Header = "Custom size..." };
        custom.Click += (_, _) => OnCustomSize();
        game.Items.Add(custom);

        var restart = new MenuItem { Header = "Restart" };
        restart.Click += (_, _) => NewGame(_rows, _cols);
        game.Items.Add(restart);

        var exit = new MenuItem { Header = "Exit" };
        exit.Click += (_, _) => Close();
        game.Items.Add(exit);

        var solve = new MenuItem { Header = "Solve" };
        var blank = new MenuItem { Header = "New board (blank)" };
        blank.Click += (_, _) => NewBlankBoard();
        solve.Items.Add(blank);

        var run = new MenuItem { Header = "Solve" };
        run.Click += (_, _) => SolveBoard();
        solve.Items.Add(run);

        var clear = new MenuItem { Header = "Clear marks" };
        clear.Click += (_, _) =>
        {
            ClearHint();
            UpdateBoard();
            UpdateStatus();
        };
        solve.Items.Add(clear);

        var help = new MenuItem { Header = "Help" };
        var about = new MenuItem { Header = "About" };
        about.Click += (_, _) => ShowAbout();
        help.Items.Add(about);

        menu.Items.Add(game);
        menu.Items.Add(solve);
        menu.Items.Add(help);
        return menu;
    }

    /* --------------------------------------------------------- game logic */

    private void NewGame(int rows, int cols)
    {
        rows = Math.Clamp(rows, MinSide, MaxSide);
        cols = Math.Clamp(cols, MinSide, MaxSide);
        _rows = rows;
        _cols = cols;

        int n = rows * cols;
        _cells = new byte[n];

        /* Random clicks on a dark board: every puzzle is solvable. */
        do
        {
            Array.Clear(_cells, 0, n);
            int pct = 30 + Random.Shared.Next(40);
            for (int i = 0; i < n; i++)
                if (Random.Shared.Next(100) < pct)
                    Press(i / cols, i % cols);
        } while (AllDark());

        _moves = 0;
        _solved = false;
        _edit = false;
        ClearHint();
        Title = $"Lights Out  -  {cols}x{rows}";
        UpdateBoard();
        UpdateStatus();
    }

    private void NewBlankBoard()
    {
        Array.Clear(_cells, 0, _cells.Length);
        _moves = 0;
        _solved = false;
        _edit = true;
        ClearHint();
        UpdateBoard();
        UpdateStatus();
    }

    private void SolveBoard()
    {
        if (_cells.Length != _rows * _cols)
            return;

        var clicks = new byte[_cells.Length];
        int cnt;
        int rc = NativeSolver.Solve(_rows, _cols, _cells, true, clicks, out cnt);

        if (rc == 1)
        {
            _hint = clicks;
            _hintCount = cnt;
            _edit = false;
            _moves = 0;
            _solved = false;
            UpdateBoard();
            UpdateStatus();
        }
        else if (rc == 0)
        {
            _ = MessageDialog.Show(this, "Solve",
                "This position cannot be turned off.\nNot every Lights Out position is solvable.");
        }
        else
        {
            _ = MessageDialog.Show(this, "Solve", "This board is too large to solve.");
        }
    }

    private void ClearHint()
    {
        _hint = null;
        _hintCount = 0;
    }

    private void OnCellPressed(int r, int c)
    {
        if (_cells.Length != _rows * _cols)
            return;

        if (_edit)
        {
            _cells[r * _cols + c] ^= 1;
        }
        else
        {
            if (_solved)
                return;
            Press(r, c);
            ClearHint();
            _moves++;
            if (AllDark())
                _solved = true;
        }

        UpdateBoard();
        UpdateStatus();
    }

    private void Press(int r, int c)
    {
        Flip(r, c);
        Flip(r - 1, c);
        Flip(r + 1, c);
        Flip(r, c - 1);
        Flip(r, c + 1);
    }

    private void Flip(int r, int c)
    {
        if (r < 0 || r >= _rows || c < 0 || c >= _cols)
            return;
        _cells[r * _cols + c] ^= 1;
    }

    private bool AllDark()
    {
        foreach (byte v in _cells)
            if (v != 0)
                return false;
        return true;
    }

    private void UpdateBoard() => _board.SetBoard(_rows, _cols, _cells, _hint);

    private void UpdateStatus()
    {
        _statusRight.Text = $"{_cols} x {_rows}";

        if (_solved)
        {
            _statusLeft.Text = $"Solved in {_moves} moves!   Press N for a new game.";
            _statusLeft.Foreground = WinBrush;
            _statusLeft.FontWeight = FontWeight.SemiBold;
        }
        else if (_hint is not null)
        {
            _statusLeft.Text = $"Solution: {_hintCount} presses  -  click the marked cells (C clears the marks)";
            _statusLeft.Foreground = HintBrush;
            _statusLeft.FontWeight = FontWeight.SemiBold;
        }
        else if (_edit)
        {
            _statusLeft.Text = "Edit mode  -  click cells to light them, then Solve -> Solve";
            _statusLeft.Foreground = TextBrush;
            _statusLeft.FontWeight = FontWeight.SemiBold;
        }
        else
        {
            _statusLeft.Text = $"Moves: {_moves}";
            _statusLeft.Foreground = TextBrush;
            _statusLeft.FontWeight = FontWeight.Normal;
        }
    }

    /* ------------------------------------------------------------ dialogs */

    private async void OnCustomSize()
    {
        var dialog = new CustomSizeDialog(_cols, _rows);
        var result = await dialog.ShowDialog<(int rows, int cols)?>(this);
        if (result is { } size)
            NewGame(size.rows, size.cols);
    }

    private async void ShowAbout()
    {
        await MessageDialog.Show(this, "About",
            "Lights Out\n\n" +
            "Click a cell to toggle it and its four neighbours.  Turn every\n" +
            "lamp off to win.\n\n" +
            "Solve menu: New board (blank) starts an empty board where a click\n" +
            "lights up that cell only; Solve marks the fewest presses that\n" +
            "clear the board.\n\n" +
            $"Solver: {NativeSolver.Version()} (C library)\n\n" +
            "Keys: N new game, R restart, B blank board, S solve, C clear\n" +
            "marks, Esc quit.");
    }

    /* -------------------------------------------------------------- input */

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.N:
            case Key.R:
                NewGame(_rows, _cols);
                e.Handled = true;
                break;
            case Key.B:
                NewBlankBoard();
                e.Handled = true;
                break;
            case Key.S:
                SolveBoard();
                e.Handled = true;
                break;
            case Key.C:
                ClearHint();
                UpdateBoard();
                UpdateStatus();
                e.Handled = true;
                break;
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
        }
    }
}
