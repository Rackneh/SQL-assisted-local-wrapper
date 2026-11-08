using MessageBox = DoryNative.XpSim.SimMessageBox;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace DoryNative.Views;

public partial class NotepadWindow : UserControl
{
    private string? _currentFile;
    private bool _isModified;

    public NotepadWindow()
    {
        InitializeComponent();
        Editor.TextChanged += (s, e) => { _isModified = true; UpdateStatus(); };
    }

    private void UpdateStatus()
    {
        var pos = Editor.CaretIndex;
        var text = Editor.Text ?? "";
        var upto = text.Substring(0, Math.Min(pos, text.Length));
        var line = upto.Split('\n').Length;
        var col = upto.Length - upto.LastIndexOf('\n');
        StatusText.Text = $"Ln {line}, Col {col}";
        ModifiedIndicator.Text = _isModified ? "* Modified" : "";
    }

    private void Editor_TextChanged(object sender, TextChangedEventArgs e) => UpdateStatus();

    private void FileMenu_Click(object sender, RoutedEventArgs e)
    {
        var tag = (sender as Button)?.Tag?.ToString();
        switch (tag)
        {
            case "file": ShowFileMenu(sender as Button); break;
            case "edit": ShowEditMenu(sender as Button); break;
            case "format": ShowFormatMenu(sender as Button); break;
        }
    }

    private void ShowFileMenu(Button anchor)
    {
        var menu = new ContextMenu();
        menu.Items.Add(MakeMenuItem("New", () => { Editor.Clear(); _currentFile = null; _isModified = false; }));
        menu.Items.Add(MakeMenuItem("Open...", OpenFile));
        menu.Items.Add(MakeMenuItem("Save", SaveFile));
        menu.Items.Add(MakeMenuItem("Save As...", SaveFileAs));
        menu.Items.Add(new Separator());
        menu.Items.Add(MakeMenuItem("Exit", () => CloseWindow()));
        menu.PlacementTarget = anchor;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void ShowEditMenu(Button anchor)
    {
        var menu = new ContextMenu();
        menu.Items.Add(MakeMenuItem("Undo", () => Editor.Undo()));
        menu.Items.Add(MakeMenuItem("Redo", () => Editor.Redo()));
        menu.Items.Add(new Separator());
        menu.Items.Add(MakeMenuItem("Cut", () => Editor.Cut()));
        menu.Items.Add(MakeMenuItem("Copy", () => Editor.Copy()));
        menu.Items.Add(MakeMenuItem("Paste", () => Editor.Paste()));
        menu.Items.Add(new Separator());
        menu.Items.Add(MakeMenuItem("Select All", () => Editor.SelectAll()));
        menu.Items.Add(MakeMenuItem("Time/Date", InsertDateTime));
        menu.PlacementTarget = anchor;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void ShowFormatMenu(Button anchor)
    {
        var menu = new ContextMenu();
        menu.Items.Add(MakeMenuItem("Word Wrap", () =>
        {
            Editor.TextWrapping = Editor.TextWrapping == TextWrapping.Wrap
                ? TextWrapping.NoWrap : TextWrapping.Wrap;
        }));
        menu.Items.Add(new Separator());
        foreach (var font in new[] { "Consolas", "Courier New", "Lucida Console", "Tahoma", "Arial" })
        {
            menu.Items.Add(MakeMenuItem(font, () => Editor.FontFamily = new FontFamily(font)));
        }
        menu.Items.Add(new Separator());
        foreach (var size in new[] { 10, 12, 14, 16, 18, 20 })
        {
            menu.Items.Add(MakeMenuItem($"{size}pt", () => Editor.FontSize = size));
        }
        menu.PlacementTarget = anchor;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private MenuItem MakeMenuItem(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (s, e) => action();
        return item;
    }

    private void InsertDateTime()
    {
        var dt = DateTime.Now.ToString("dddd, MMMM d, yyyy h:mm:ss tt");
        Editor.SelectedText = dt;
        Editor.CaretIndex += dt.Length;
    }

    private void OpenFile()
    {
        var dlg = new OpenFileDialog { Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*" };
        if (dlg.ShowDialog() == true)
        {
            try
            {
                Editor.Text = File.ReadAllText(dlg.FileName);
                _currentFile = dlg.FileName;
                _isModified = false;
                UpdateStatus();
            }
            catch (Exception ex)
            {
                DoryNative.XpSim.SimMessageBox.Show($"Cannot open file: {ex.Message}");
            }
        }
    }

    private void SaveFile()
    {
        if (_currentFile == null) { SaveFileAs(); return; }
        try
        {
            File.WriteAllText(_currentFile, Editor.Text);
            _isModified = false;
            UpdateStatus();
        }
        catch (Exception ex)
        {
            DoryNative.XpSim.SimMessageBox.Show($"Cannot save: {ex.Message}");
        }
    }

    private void SaveFileAs()
    {
        var dlg = new SaveFileDialog { Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*" };
        if (dlg.ShowDialog() == true)
        {
            try
            {
                File.WriteAllText(dlg.FileName, Editor.Text);
                _currentFile = dlg.FileName;
                _isModified = false;
                UpdateStatus();
            }
            catch (Exception ex)
            {
                DoryNative.XpSim.SimMessageBox.Show($"Cannot save: {ex.Message}");
            }
        }
    }

    private void CloseWindow()
    {
        var parent = FindParentXpWindow();
        parent?.TriggerClose();
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
