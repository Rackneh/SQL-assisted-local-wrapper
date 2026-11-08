using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DoryNative.Services;

namespace DoryNative.Views;

public partial class ExplorerWindow : UserControl
{
    private readonly BackendApi _backend;
    private readonly ObservableCollection<FileItem> _files = new();
    private readonly Stack<string> _history = new();
    private string _currentPath = "";
    private readonly HttpClient _http = new() { BaseAddress = new Uri("http://127.0.0.1:5000") };

    public ExplorerWindow(BackendApi backend)
    {
        InitializeComponent();
        _backend = backend;
        FileList.ItemsSource = _files;
        Loaded += async (s, e) => await NavigateAsync(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    }

    private async Task NavigateAsync(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        if (_currentPath.Length > 0) _history.Push(_currentPath);
        _currentPath = path;
        PathBox.Text = path;
        StatusText.Text = $"Loading {path}...";

        try
        {
            var resp = await _http.GetAsync($"/api/iba/fs/browse?path={Uri.EscapeDataString(path)}");
            if (!resp.IsSuccessStatusCode)
            {
                // Fallback: browse local filesystem directly
                await BrowseLocalAsync(path);
                return;
            }
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            _files.Clear();

            if (doc.RootElement.TryGetProperty("entries", out var entries))
            {
                foreach (var entry in entries.EnumerateArray())
                {
                    var name = entry.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    var isDir = entry.TryGetProperty("is_dir", out var d) && d.GetBoolean();
                    var size = entry.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0;
                    var fullPath = Path.Combine(path, name);
                    _files.Add(new FileItem
                    {
                        Name = (isDir ? "📁 " : "📄 ") + name,
                        FullPath = fullPath,
                        IsDirectory = isDir,
                        SizeText = isDir ? "" : FormatSize(size),
                        ModifiedText = DateTime.Now.ToString("MM/dd/yyyy hh:mm tt")
                    });
                }
            }
            StatusText.Text = $"{_files.Count} items";
            RebuildTree(path);
        }
        catch
        {
            await BrowseLocalAsync(path);
        }
    }

    private async Task BrowseLocalAsync(string path)
    {
        try
        {
            _files.Clear();
            var dir = new DirectoryInfo(path);
            foreach (var d in dir.GetDirectories().OrderBy(x => x.Name))
            {
                _files.Add(new FileItem
                {
                    Name = "📁 " + d.Name,
                    FullPath = d.FullName,
                    IsDirectory = true,
                    SizeText = "",
                    ModifiedText = d.LastWriteTime.ToString("MM/dd/yyyy hh:mm tt")
                });
            }
            foreach (var f in dir.GetFiles().OrderBy(x => x.Name))
            {
                _files.Add(new FileItem
                {
                    Name = "📄 " + f.Name,
                    FullPath = f.FullName,
                    IsDirectory = false,
                    SizeText = FormatSize(f.Length),
                    ModifiedText = f.LastWriteTime.ToString("MM/dd/yyyy hh:mm tt")
                });
            }
            StatusText.Text = $"{_files.Count} items (local)";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
        await Task.CompletedTask;
    }

    private void RebuildTree(string path)
    {
        var tree = DirTree;
        tree.Items.Clear();
        var root = new TreeViewItem { Header = Path.GetPathRoot(path) ?? path, Tag = Path.GetPathRoot(path) };
        tree.Items.Add(root);
        BuildTreeChildren(root, path, 0);
        root.IsExpanded = true;
    }

    private void BuildTreeChildren(TreeViewItem parent, string dirPath, int depth)
    {
        if (depth > 3) return;
        try
        {
            foreach (var dir in new DirectoryInfo(dirPath).GetDirectories().OrderBy(x => x.Name).Take(30))
            {
                var item = new TreeViewItem { Header = dir.Name, Tag = dir.FullName };
                parent.Items.Add(item);
                try { BuildTreeChildren(item, dir.FullName, depth + 1); } catch { }
            }
        }
        catch { }
    }

    private void DirTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is TreeViewItem item && item.Tag is string path)
        {
            _ = NavigateAsync(path);
        }
    }

    private void FileList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FileList.SelectedItem is FileItem fi)
        {
            if (fi.IsDirectory) _ = NavigateAsync(fi.FullPath);
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_history.Count > 0) _ = NavigateAsync(_history.Pop());
    }

    private void Up_Click(object sender, RoutedEventArgs e)
    {
        var parent = Directory.GetParent(_currentPath)?.FullName;
        if (!string.IsNullOrEmpty(parent)) _ = NavigateAsync(parent);
    }

    private void Go_Click(object sender, RoutedEventArgs e) => _ = NavigateAsync(PathBox.Text.Trim());

    private void PathBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) _ = NavigateAsync(PathBox.Text.Trim());
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F1} GB"
    };

    public class FileItem
    {
        public string Name { get; set; } = "";
        public string FullPath { get; set; } = "";
        public bool IsDirectory { get; set; }
        public string SizeText { get; set; } = "";
        public string ModifiedText { get; set; } = "";
    }
}
