using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DoryNative.Services;

namespace DoryNative.Views;

public partial class CmdWindow : UserControl
{
    private readonly BackendApi _backend;
    private string _cwd = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private readonly List<string> _history = new();
    private int _historyIndex = -1;

    public CmdWindow(BackendApi backend)
    {
        InitializeComponent();
        _backend = backend;
        UpdatePrompt();
        Loaded += (s, e) => InputBox.Focus();
    }

    private void UpdatePrompt() => PromptText.Text = _cwd + ">";

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var cmd = InputBox.Text.Trim();
            InputBox.Clear();
            if (cmd.Length == 0) return;
            _history.Add(cmd);
            _historyIndex = _history.Count;
            ExecuteCommand(cmd);
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            if (_historyIndex > 0)
            {
                _historyIndex--;
                InputBox.Text = _history[_historyIndex];
                InputBox.CaretIndex = InputBox.Text.Length;
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            if (_historyIndex < _history.Count - 1)
            {
                _historyIndex++;
                InputBox.Text = _history[_historyIndex];
            }
            else
            {
                _historyIndex = _history.Count;
                InputBox.Text = "";
            }
            e.Handled = true;
        }
    }

    private void PrintLine(string text, Brush? color = null)
    {
        OutputPanel.Children.Add(new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            Foreground = color ?? Brushes.White,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 1, 0, 1)
        });
        OutputScroll.ScrollToEnd();
    }

    private void ExecuteCommand(string cmd)
    {
        PrintLine(_cwd + ">" + cmd, new SolidColorBrush(Color.FromRgb(0x9C, 0xDC, 0xFE)));

        var parts = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var verb = parts[0].ToLowerInvariant();

        switch (verb)
        {
            case "help":
                PrintLine("Available commands:");
                PrintLine("  dir / ls       List directory contents");
                PrintLine("  cd <path>      Change directory");
                PrintLine("  echo <text>    Print text");
                PrintLine("  type <file>    Print file contents");
                PrintLine("  cls            Clear screen");
                PrintLine("  ver            Show version");
                PrintLine("  pwd            Print working directory");
                PrintLine("  whoami         Current user");
                PrintLine("  date / time    Show date/time");
                PrintLine("  exit           Close this window");
                PrintLine("  status         Show backend status");
                PrintLine("  sessions       List IBA sessions");
                PrintLine("  tasks          List IBA tasks");
                PrintLine("  provider       Show provider status");
                break;

            case "cls":
                OutputPanel.Children.Clear();
                break;

            case "ver":
                PrintLine("\nMicrosoft Windows XP [Version 5.1.2600]\n");
                break;

            case "pwd":
                PrintLine(_cwd);
                break;

            case "whoami":
                PrintLine(Environment.UserName);
                break;

            case "date":
                PrintLine(DateTime.Now.ToLongDateString());
                break;

            case "time":
                PrintLine(DateTime.Now.ToLongTimeString());
                break;

            case "echo":
                PrintLine(string.Join(' ', parts.Skip(1)));
                break;

            case "dir":
            case "ls":
                try
                {
                    foreach (var d in Directory.GetDirectories(_cwd).OrderBy(x => x))
                        PrintLine($"{Directory.GetLastWriteTime(d):MM/dd/yyyy  hh:mm tt}    <DIR>          {Path.GetFileName(d)}");
                    foreach (var f in Directory.GetFiles(_cwd).OrderBy(x => x))
                    {
                        var fi = new FileInfo(f);
                        PrintLine($"{fi.LastWriteTime:MM/dd/yyyy  hh:mm tt}    {fi.Length,12}    {fi.Name}");
                    }
                }
                catch (Exception ex) { PrintLine($"Error: {ex.Message}", Brushes.Red); }
                break;

            case "cd":
                var target = parts.Length > 1 ? string.Join(' ', parts.Skip(1)) : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (target == "..") target = Directory.GetParent(_cwd)?.FullName ?? _cwd;
                else if (!Path.IsPathRooted(target)) target = Path.Combine(_cwd, target);
                try { if (Directory.Exists(target)) { _cwd = target; UpdatePrompt(); } else PrintLine("The system cannot find the path specified.", Brushes.Red); }
                catch { PrintLine("Access denied.", Brushes.Red); }
                break;

            case "type":
                if (parts.Length < 2) { PrintLine("The syntax of the command is incorrect.", Brushes.Red); break; }
                try
                {
                    var fp = Path.IsPathRooted(parts[1]) ? parts[1] : Path.Combine(_cwd, parts[1]);
                    var content = File.ReadAllText(fp);
                    foreach (var line in content.Split('\n')) PrintLine(line.TrimEnd('\r'));
                }
                catch (Exception ex) { PrintLine($"Cannot find file or directory: {parts[1]}", Brushes.Red); }
                break;

            case "exit":
                FindParentXpWindow()?.TriggerClose();
                break;

            case "status":
                _ = ShowStatusAsync();
                break;

            case "sessions":
                _ = ShowSessionsAsync();
                break;

            case "tasks":
                _ = ShowTasksAsync();
                break;

            case "provider":
                _ = ShowProviderAsync();
                break;

            default:
                // Try as a real shell command
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c {cmd}",
                        WorkingDirectory = _cwd,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var proc = Process.Start(psi);
                    if (proc != null)
                    {
                        var output = proc.StandardOutput.ReadToEnd();
                        var error = proc.StandardError.ReadToEnd();
                        proc.WaitForExit(10000);
                        if (!string.IsNullOrEmpty(output)) PrintLine(output.TrimEnd());
                        if (!string.IsNullOrEmpty(error)) PrintLine(error.TrimEnd(), Brushes.Red);
                        if (string.IsNullOrEmpty(output) && string.IsNullOrEmpty(error))
                            PrintLine($"'{verb}' is not recognized as an internal or external command.", Brushes.Red);
                    }
                }
                catch
                {
                    PrintLine($"'{verb}' is not recognized as an internal or external command.", Brushes.Red);
                }
                break;
        }
    }

    private async System.Threading.Tasks.Task ShowStatusAsync()
    {
        try
        {
            var status = await _backend.GetStatusAsync();
            string Get(string key) => status.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
            PrintLine($"Engine: {Get("status")} | Model: {Get("model_file")} | Port: {Get("port")}");
        }
        catch (Exception ex) { PrintLine($"Backend error: {ex.Message}", Brushes.Red); }
    }

    private async System.Threading.Tasks.Task ShowSessionsAsync()
    {
        try
        {
            var sessions = await _backend.GetSessionsAsync();
            PrintLine($"Active: {sessions.ActiveSession} ({sessions.Sessions.Count} sessions)");
            foreach (var s in sessions.Sessions)
                PrintLine($"  - {s.SessionId}: {s.Persona?.Name ?? "(no persona)"}");
        }
        catch (Exception ex) { PrintLine($"Backend error: {ex.Message}", Brushes.Red); }
    }

    private async System.Threading.Tasks.Task ShowTasksAsync()
    {
        try
        {
            var tasks = await _backend.GetTasksAsync();
            PrintLine($"{tasks.Count} task(s)");
            foreach (var t in tasks.Take(10))
                PrintLine($"  [{t.Status}] {t.Id[..Math.Min(8, t.Id.Length)]}: {t.Objective}");
        }
        catch (Exception ex) { PrintLine($"Backend error: {ex.Message}", Brushes.Red); }
    }

    private async System.Threading.Tasks.Task ShowProviderAsync()
    {
        try
        {
            var p = await _backend.GetProviderAsync();
            PrintLine($"Provider: {p.Provider} | LM Model: {p.LmModel ?? "none"}");
            PrintLine($"Dory running: {p.DoryRunning} | LM Studio loaded: {string.Join(", ", p.LmStudioLoaded)}");
        }
        catch (Exception ex) { PrintLine($"Backend error: {ex.Message}", Brushes.Red); }
    }

    private XpWindow? FindParentXpWindow()
    {
        DependencyObject current = this;
        while (current != null)
        {
            if (current is XpWindow xp) return xp;
            current = System.Windows.Media.VisualTreeHelper.GetParent(current)
                      ?? (current as FrameworkElement)?.Parent;
        }
        return null;
    }
}
