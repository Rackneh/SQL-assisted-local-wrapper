using MessageBox = DoryNative.XpSim.SimMessageBox;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using DoryNative.XpSim;

namespace DoryNative.Views;

/// <summary>Windows XP Task Manager — Applications/Processes/Performance tabs.</summary>
public partial class TaskMgrControl : UserControl
{
    private class AppRow { public string Name { get; set; } = ""; public string Status { get; set; } = "Running"; }
    private class ProcRow { public string Name { get; set; } = ""; public int Pid { get; set; } public string Cpu { get; set; } = ""; public string Mem { get; set; } = ""; }

    private readonly DispatcherTimer _refresh = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly List<double> _cpuHistory = new();
    private readonly List<double> _memHistory = new();
    private readonly Random _rng = new();

    public TaskMgrControl()
    {
        InitializeComponent();
        RefreshApps();
        RefreshProcs();
        _refresh.Tick += (_, _) => { RefreshApps(); RefreshProcs(); UpdateGraphs(); };
        _refresh.Start();
        Loaded += (_, _) => UpdateGraphs();
    }

    private void RefreshApps()
    {
        AppsGrid.ItemsSource = new List<AppRow>
        {
            new() { Name = "Windows Live Messenger" },
            new() { Name = "Windows Explorer" },
            new() { Name = "Command Prompt" },
            new() { Name = "Notepad" },
            new() { Name = "Solitaire" },
            new() { Name = "Minesweeper" },
        };
    }

    private void RefreshProcs()
    {
        var procs = new List<ProcRow>
        {
            new() { Name = "System Idle Process", Pid = 0, Cpu = "99", Mem = "16 K" },
            new() { Name = "System", Pid = 4, Cpu = "0", Mem = "220 K" },
            new() { Name = "smss.exe", Pid = 500, Cpu = "0", Mem = "380 K" },
            new() { Name = "csrss.exe", Pid = 548, Cpu = "0", Mem = "4,900 K" },
            new() { Name = "winlogon.exe", Pid = 560, Cpu = "0", Mem = "3,800 K" },
            new() { Name = "services.exe", Pid = 592, Cpu = "0", Mem = "3,200 K" },
            new() { Name = "lsass.exe", Pid = 604, Cpu = "0", Mem = "5,600 K" },
            new() { Name = "svchost.exe", Pid = 688, Cpu = "0", Mem = "4,100 K" },
            new() { Name = "explorer.exe", Pid = 1204, Cpu = "1", Mem = "18,400 K" },
            new() { Name = "msnmsgr.exe", Pid = 1452, Cpu = "2", Mem = "42,300 K" },
            new() { Name = "DoryNative.exe", Pid = System.Diagnostics.Process.GetCurrentProcess().Id, Cpu = "3", Mem = "96,000 K" },
        };
        ProcsGrid.ItemsSource = procs;
    }

    private void UpdateGraphs()
    {
        _cpuHistory.Add(20 + _rng.NextDouble() * 60);
        _memHistory.Add(45 + _rng.NextDouble() * 20);
        if (_cpuHistory.Count > 60) _cpuHistory.RemoveAt(0);
        if (_memHistory.Count > 60) _memHistory.RemoveAt(0);
        DrawGraph(CpuGraph, _cpuHistory, 100);
        DrawGraph(MemGraph, _memHistory, 100);
    }

    private void DrawGraph(Polyline line, List<double> data, double max)
    {
        line.Points.Clear();
        if (data.Count < 2) return;
        double w = ((FrameworkElement)line.Parent!).ActualWidth;
        double h = ((FrameworkElement)line.Parent!).ActualHeight;
        if (w <= 0 || h <= 0) { w = 380; h = 52; }
        for (int i = 0; i < data.Count; i++)
        {
            double x = i * w / 59.0;
            double y = h - (data[i] / max) * h;
            line.Points.Add(new Point(x, y));
        }
    }

    private void Tab_Changed(object sender, RoutedEventArgs e)
    {
        AppsGrid.Visibility = TabApps.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ProcsGrid.Visibility = TabProcs.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PerfPanel.Visibility = TabPerf.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void EndTask_Click(object sender, RoutedEventArgs e)
    {
        if (ProcsGrid.SelectedItem is ProcRow p && p.Pid != 0 && p.Pid != 4)
        {
            if (DoryNative.XpSim.SimMessageBox.Show($"End task '{p.Name}'?", "Task Manager",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                RefreshProcs();
        }
    }

    private void NewTask_Click(object sender, RoutedEventArgs e) => XpAudio.Click();
}
