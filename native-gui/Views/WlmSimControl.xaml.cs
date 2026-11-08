using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DoryNative.Models;
using DoryNative.Services;
using WLMRevivalV2Desktop.Protocol;
using MSNPSharp;

namespace DoryNative.Views;

/// <summary>
/// Windows Live Messenger as an in-sim app: IBA session/contact list on the
/// left, chat with reasoning/tool/MCQ/parallel rendering on the right.
/// Uses MessengerService (SSE -> IbaEventReceived) and session history.
/// </summary>
public partial class WlmSimControl : UserControl
{
    private readonly MessengerService _messenger;
    private readonly BackendApi _backend;
    private Contact? _contact;

    // ---- IBA rendering state (mirrors ChatWindowLive) ----
    private class IbaTaskView
    {
        public string TaskId = "";
        public string TaskTitle = "";
        public Border? Column;
        public StackPanel? Body;
        public TextBlock? Header;
        public TextBlock? ObjectiveLine;
        public Expander? OpenThink;
        public TextBlock? OpenThinkBody;
        public string ThinkText = "";
        public int ThinkTokens;
        public readonly Dictionary<string, IbaToolState> Tools = new();
        public DateTime LastActivity = DateTime.MinValue;
        public bool HasPendingTool;
        public bool Waiting;
        public bool Completed;
    }
    private class IbaToolState
    {
        public string Step = "";
        public Border? Element;
        public string ToolName = "";
        public string Status = "running";
        public string ArgsJson = "{}";
        public string ResultText = "";
        public string Title = "";
    }

    private readonly Dictionary<string, IbaTaskView> _ibaViews = new();
    private StackPanel? _ibaHost;
    private static readonly TimeSpan IbaParallelWindow = TimeSpan.FromSeconds(30);
    private readonly HashSet<string> _renderedHistoryKeys = new();
    private bool _loadingHistory;
    private EventHandler? _contactsSyncHandler;

    private sealed class ContactRow
    {
        public string SessionId = "";
        public string Name = "";
        public string Subtitle = "";
        public ImageSource? AvatarImage;
        public Contact? Contact;
    }

    public WlmSimControl(MessengerService messenger, BackendApi backend)
    {
        InitializeComponent();
        _messenger = messenger;
        _backend = backend;
        _messenger.IbaEventReceived += OnIbaEvent;
        _messenger.MessageReceived += OnMessage;
        _contactsSyncHandler = (_, _) => Dispatcher.BeginInvoke(RefreshContacts);
        _messenger.ContactListSynchronized += _contactsSyncHandler;
        Unloaded += (_, _) =>
        {
            _messenger.IbaEventReceived -= OnIbaEvent;
            _messenger.MessageReceived -= OnMessage;
            _messenger.ContactListSynchronized -= _contactsSyncHandler;
        };
        RefreshContacts();
    }

    // ---------------- contacts ----------------

    private void RefreshContacts()
    {
        var rows = _messenger.ForwardList
            .Select(c => new ContactRow
            {
                SessionId = c.Mail,
                Name = string.IsNullOrEmpty(c.Name) ? c.Mail : c.Name,
                Subtitle = c.PersonalMessage?.Message is { Length: > 0 } b
                    ? (b.Length > 60 ? b[..60] + "…" : b) : "online",
                AvatarImage = c.Tag as ImageSource,
                Contact = c
            })
            .OrderBy(r => r.Name)
            .ToList();
        ContactList.ItemsSource = rows;
        if (_contact == null && rows.Count > 0) SelectContact(rows[0].Contact);
    }

    private void ContactList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ContactList.SelectedItem is ContactRow row) SelectContact(row.Contact);
    }

    private async void SelectContact(Contact? contact)
    {
        if (contact == null) return;
        _contact = contact;
        ChatTitle.Text = contact.Name ?? contact.Mail;
        ChatSubtitle.Text = contact.PersonalMessage?.Message ?? "";
        MessagesPanel.Children.Clear();
        _ibaViews.Clear();
        _ibaHost = null;
        _toolElements.Clear();
        _renderedHistoryKeys.Clear();

        AddSystemLine($"— {contact.Name} —");
        await LoadHistoryAsync(contact.Mail);
    }

    private async Task LoadHistoryAsync(string sessionId)
    {
        _loadingHistory = true;
        try
        {
            var hist = await _messenger.GetSessionHistoryAsync(sessionId);
            if (hist == null || _contact?.Mail != sessionId) return;
            await Dispatcher.InvokeAsync(() => RenderIbaHistory(hist));
            MessagesScroller.ScrollToEnd();
        }
        catch { }
        finally { _loadingHistory = false; }
    }

    // ---------------- messaging ----------------

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { Send_Click(sender, e); e.Handled = true; }
    }

    private void Send_Click(object sender, RoutedEventArgs e)
    {
        var text = InputBox.Text.Trim();
        if (text.Length == 0 || _contact == null) return;
        InputBox.Clear();
        AddBubble(text, true);
        _messenger.SendMessage(_contact, text);
    }

    private void OnMessage(object? sender, IncomingMessageEventArgs e)
    {
        if (e.From == null || _contact == null || e.From.Mail != _contact.Mail) return;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (e.Text != null) AddBubble(e.Text, false);
            MessagesScroller.ScrollToEnd();
        }));
    }

    private void OnIbaEvent(object? sender, IbaEventArgs e)
    {
        if (e.From == null || _contact == null || e.From.Mail != _contact.Mail) return;
        var name = e.EventName; var data = e.Data;
        Dispatcher.BeginInvoke(new Action(() => { RenderIbaEvent(name, data); MessagesScroller.ScrollToEnd(); }));
    }

    private void AddBubble(string text, bool mine)
    {
        var bubble = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 7, 10, 7),
            MaxWidth = 460,
            Margin = new Thickness(0, 3, 0, 3),
            HorizontalAlignment = mine ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            Background = mine
                ? new LinearGradientBrush(Color.FromRgb(0x00, 0x54, 0xE3), Color.FromRgb(0x3A, 0x75, 0xD4), 45)
                : Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xBF, 0xD3, 0xE6)),
            BorderThickness = new Thickness(1)
        };
        bubble.Child = new TextBlock
        {
            Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 12,
            FontFamily = new FontFamily("Tahoma"),
            Foreground = mine ? Brushes.White : Brushes.Black
        };
        MessagesPanel.Children.Add(bubble);
    }

    private void AddSystemLine(string text)
    {
        MessagesPanel.Children.Add(new TextBlock
        {
            Text = text, FontSize = 10, FontStyle = FontStyles.Italic,
            Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
            HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 6)
        });
    }

    // ---------------- history ----------------

    private void RenderIbaHistory(SessionHistoryResponse hist)
    {
        foreach (var msg in hist.Messages)
        {
            var key = $"{msg.Role}|{msg.TaskId}|{msg.Timestamp}";
            if (!_renderedHistoryKeys.Add(key)) continue;

            if (string.Equals(msg.Role, "user", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(msg.Content)) AddBubble(msg.Content, true);
                continue;
            }

            var groups = msg.StepGroups;
            if ((groups == null || groups.Count == 0) && msg.Steps?.Count > 0)
            {
                groups = msg.Steps
                    .GroupBy(s => string.IsNullOrEmpty(s.TaskId) ? msg.TaskId : s.TaskId)
                    .Select(g => new IbaStepGroup { TaskId = g.Key, Title = msg.TaskTitle, Steps = g.ToList() })
                    .ToList();
            }
            if (groups != null)
                foreach (var g in groups) RenderIbaHistoryGroup(g, msg.TaskTitle);

            if (!string.IsNullOrEmpty(msg.Content)) AddBubble(msg.Content, false);
        }
    }

    private void RenderIbaHistoryGroup(IbaStepGroup g, string fallbackTitle)
    {
        var v = EnsureIbaView(g.TaskId, string.IsNullOrEmpty(g.Title) ? fallbackTitle : g.Title);
        v.Completed = true; v.LastActivity = DateTime.MinValue;
        foreach (var step in g.Steps)
        {
            if (!string.IsNullOrEmpty(step.Reasoning)) { AppendThink(v, step.Reasoning); CloseThink(v); }
            if (string.IsNullOrEmpty(step.ActionName)) continue;
            AddIbaLine(v, $"— Step {step.StepNumber}: {step.StepTitle} —");
            string argsJson = "{}";
            if (step.ActionArgs?.Count > 0)
                try { argsJson = JsonSerializer.Serialize(step.ActionArgs); } catch { }
            bool failed = string.Equals(step.ActionState, "failed", StringComparison.OrdinalIgnoreCase);
            var ts = new IbaToolState
            {
                Step = step.StepId, ToolName = step.ActionName, Status = failed ? "failed" : "completed",
                ArgsJson = argsJson, Title = step.StepTitle,
                ResultText = !string.IsNullOrEmpty(step.ActionResultBody)
                    ? (step.ActionResultBody.Length > 3000 ? step.ActionResultBody[..3000] + "\n… (truncated)" : step.ActionResultBody)
                    : argsJson
            };
            v.Body!.Children.Add(BuildToolCard(ts));
            v.Tools[ts.Step] = ts;
        }
        CloseThink(v);
        SyncIbaLayout(v);
    }

    // ---------------- live IBA events ----------------

    private static string JStr(JsonElement d, string name)
        => d.ValueKind == JsonValueKind.Object && d.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString() ?? "" : "";

    private static int JInt(JsonElement d, string name)
        => d.ValueKind == JsonValueKind.Object && d.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var i) ? i : 0;

    private static string PrettyJson(JsonElement el)
    {
        try { return JsonSerializer.Serialize(el, new JsonSerializerOptions { WriteIndented = true }); }
        catch { return el.ToString(); }
    }

    private static string Trim(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return s ?? "";
        s = s.Replace("\n", " ").Replace("\r", " ");
        return s.Length <= max ? s : s[..max] + "…";
    }

    private void RenderIbaEvent(string name, JsonElement d)
    {
        if (d.ValueKind != JsonValueKind.Object) return;
        string taskId = JStr(d, "task"); if (taskId.Length == 0) taskId = JStr(d, "task_id");
        string taskTitle = JStr(d, "task_title");

        switch (name)
        {
            case "answer": case "error": case "done": return;

            case "iba_task_init":
            {
                var v = EnsureIbaView(taskId, taskTitle);
                var obj = JStr(d, "objective");
                if (obj.Length > 0 && (v.ObjectiveLine?.Text.Length ?? 0) == 0)
                    v.ObjectiveLine!.Text = "▸ " + Trim(obj, 180);
                SyncIbaLayout(v);
                return;
            }
            case "iba_step_start":
            {
                var v = EnsureIbaView(taskId, taskTitle);
                CloseThink(v);
                AddIbaLine(v, $"— Step {JInt(d, "step_number")}: {JStr(d, "step_title")} —");
                SyncIbaLayout(v);
                return;
            }
            case "thought":
            {
                var v = EnsureIbaView(taskId, taskTitle);
                AppendThink(v, JStr(d, "content"));
                SyncIbaLayout(v);
                return;
            }
            case "iba_tool_start":
            {
                var v = EnsureIbaView(taskId, taskTitle);
                CloseThink(v);
                string step = JStr(d, "step"); if (step.Length == 0) step = "s" + v.Tools.Count;
                string tool = JStr(d, "tool"); if (tool.Length == 0) tool = "tool";
                string argsJson = d.TryGetProperty("args", out var a) && a.ValueKind != JsonValueKind.Undefined ? PrettyJson(a) : "{}";
                var ts = new IbaToolState
                {
                    Step = step, ToolName = tool, Status = "running",
                    ArgsJson = argsJson, Title = JStr(d, "step_title") is { Length: > 0 } t ? t : tool
                };
                v.Body!.Children.Add(BuildToolCard(ts));
                v.Tools[step] = ts;
                v.HasPendingTool = true;
                SyncIbaLayout(v);
                return;
            }
            case "iba_tool_result":
            {
                var v = EnsureIbaView(taskId, taskTitle);
                string step = JStr(d, "step");
                bool failed = string.Equals(JStr(d, "status"), "failed", StringComparison.OrdinalIgnoreCase);
                v.HasPendingTool = false;
                if (v.Tools.TryGetValue(step, out var ts))
                {
                    ts.Status = failed ? "failed" : "completed";
                    string resJson = d.TryGetProperty("result", out var r) ? PrettyJson(r) : "";
                    if (resJson.Length > 3000) resJson = resJson[..3000] + "\n… (truncated)";
                    ts.ResultText = resJson;
                    var el = ToolElementFor(v, ts);
                    if (el != null)
                    {
                        var i = v.Body!.Children.IndexOf(el);
                        var nu = BuildToolCard(ts);
                        if (i >= 0) { v.Body.Children.RemoveAt(i); v.Body.Children.Insert(i, nu); }
                    }
                }
                else AddIbaLine(v, $"✓ tool — {(failed ? "failed" : "completed")}");
                SyncIbaLayout(v);
                return;
            }
            case "iba_step_completed":
            {
                var v = EnsureIbaView(taskId, taskTitle);
                if (string.Equals(JStr(d, "status"), "failed", StringComparison.OrdinalIgnoreCase))
                    AddIbaLine(v, "✗ Step failed", true);
                v.Completed = true;
                SyncIbaLayout(v);
                return;
            }
            case "iba_wait":
            {
                var v = EnsureIbaView(taskId, taskTitle);
                CloseThink(v);
                v.Waiting = true;
                string q = JStr(d, "text"); if (q.Length == 0) q = "The agent has a question:";
                string[]? opts = null;
                if (d.TryGetProperty("options", out var op) && op.ValueKind == JsonValueKind.Array)
                {
                    var list = new List<string>();
                    foreach (var o in op.EnumerateArray())
                        if (o.ValueKind == JsonValueKind.String) list.Add(o.GetString() ?? "");
                    if (list.Count > 0) opts = list.ToArray();
                }
                v.Body!.Children.Add(BuildMcqCard(taskId, q, opts ?? new[] { "Continue", "Stop" }));
                SyncIbaLayout(v);
                return;
            }
        }
    }

    private readonly Dictionary<IbaToolState, Border> _toolElements = new();
    private Border? ToolElementFor(IbaTaskView v, IbaToolState ts)
        => _toolElements.TryGetValue(ts, out var b) ? b : null;

    private void AddIbaLine(IbaTaskView v, string text, bool isError = false)
    {
        v.Body!.Children.Add(new TextBlock
        {
            Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 10, FontStyle = FontStyles.Italic,
            Foreground = isError ? Brush("#C03030") : Brush("#888888"),
            Margin = new Thickness(0, 3, 0, 3)
        });
    }

    private void AppendThink(IbaTaskView v, string token)
    {
        if (string.IsNullOrEmpty(token)) return;
        if (v.OpenThink == null)
        {
            var exp = new Expander
            {
                IsExpanded = true, FontSize = 11, FontStyle = FontStyles.Italic,
                Foreground = Brush("#888888"), Header = "Reasoning...",
                Margin = new Thickness(0, 2, 0, 4), HorizontalAlignment = HorizontalAlignment.Left
            };
            var tb = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap, FontSize = 11,
                Foreground = Brush("#666666"), Margin = new Thickness(4, 2, 0, 0), MaxWidth = 430
            };
            exp.Content = tb;
            v.Body!.Children.Add(exp);
            v.OpenThink = exp; v.OpenThinkBody = tb; v.ThinkText = ""; v.ThinkTokens = 0;
        }
        v.ThinkText += token;
        v.OpenThinkBody!.Text = v.ThinkText;
        v.ThinkTokens += token.Split(new[] { ' ', '\n', '\t', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
        v.OpenThink.Header = $"Reasoning ({v.ThinkTokens} tokens)";
    }

    private void CloseThink(IbaTaskView v)
    {
        if (v.OpenThink != null) { v.OpenThink.IsExpanded = false; v.OpenThink = null; v.OpenThinkBody = null; }
    }

    private Border BuildToolCard(IbaToolState ts)
    {
        bool running = ts.Status == "running";
        bool failed = ts.Status == "failed";
        var statusColor = running ? "#B08000" : failed ? "#C03030" : "#2E8B2E";
        var statusGlyph = running ? "○" : failed ? "✗" : "✓";

        var header = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new TextBlock
        {
            Text = $"{statusGlyph} {ts.ToolName}", FontWeight = FontWeights.Bold, FontSize = 11,
            Foreground = Brush("#333333"), VerticalAlignment = VerticalAlignment.Center
        });
        header.Children.Add(new TextBlock
        {
            Text = running ? "running…" : ts.Status,
            FontSize = 10, Foreground = Brush(statusColor),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetColumn(header.Children[1], 2);

        var body = new StackPanel();
        if (!string.IsNullOrEmpty(ts.Title) && ts.Title != ts.ToolName)
            body.Children.Add(new TextBlock { Text = ts.Title, FontSize = 10, Foreground = Brush("#666666"), TextWrapping = TextWrapping.Wrap });
        var detail = new TextBlock
        {
            Text = running ? ts.ArgsJson : ts.ResultText,
            FontFamily = new FontFamily("Consolas"), FontSize = 10,
            TextWrapping = TextWrapping.Wrap, Foreground = Brush("#444444"),
            Margin = new Thickness(0, 3, 0, 0), MaxHeight = 140
        };
        if (running || ts.ResultText.Length > 200 || ts.ArgsJson.Length > 200)
        {
            var exp = new Expander { Header = running ? "arguments" : "result", IsExpanded = !running && ts.ResultText.Length < 500, FontSize = 10 };
            exp.Content = detail;
            body.Children.Add(exp);
        }
        else body.Children.Add(detail);

        var card = new Border
        {
            BorderBrush = Brush("#C9D6E4"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6), Background = Brush("#F6F9FC"),
            Padding = new Thickness(10, 7, 10, 7), Margin = new Thickness(0, 2, 0, 4),
            MaxWidth = 480, HorizontalAlignment = HorizontalAlignment.Left,
            Child = new StackPanel { Children = { header, body } }
        };
        _toolElements[ts] = card;
        return card;
    }

    private Border BuildMcqCard(string mcqId, string question, string[] options)
    {
        var sp = new StackPanel { Margin = new Thickness(0, 4, 0, 6) };
        sp.Children.Add(new TextBlock { Text = question, FontSize = 12, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Foreground = Brush("#333") });
        var row = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        char letter = 'A';
        foreach (var opt in options)
        {
            var choice = opt;
            var btn = new Button
            {
                Content = $"{letter++}. {choice}", Margin = new Thickness(0, 0, 6, 4), Padding = new Thickness(10, 4, 10, 4),
                FontSize = 11, FontFamily = new FontFamily("Tahoma"),
                Background = Brush("#FFFFFF"), BorderBrush = Brush("#8FB2D9"), BorderThickness = new Thickness(1), Cursor = Cursors.Hand
            };
            btn.Click += (_, _) =>
            {
                if (_contact != null) _messenger.SendMessage(_contact, choice);
            };
            row.Children.Add(btn);
        }
        sp.Children.Add(row);
        return new Border
        {
            BorderBrush = Brush("#E0A33C"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6), Background = Brush("#FFF8E8"),
            Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(0, 2, 0, 4),
            MaxWidth = 480, HorizontalAlignment = HorizontalAlignment.Left,
            Child = sp
        };
    }

    // ---------------- IBA column layout ----------------

    private IbaTaskView EnsureIbaView(string taskId, string title)
    {
        if (taskId.Length == 0) taskId = "main";
        if (!_ibaViews.TryGetValue(taskId, out var v))
        {
            v = new IbaTaskView { TaskId = taskId };
            _ibaViews[taskId] = v;
        }
        v.LastActivity = DateTime.Now;
        EnsureIbaColumn(v);
        if (title.Length > 0 && v.TaskTitle != title) { v.TaskTitle = title; v.Header!.Text = title; }
        return v;
    }

    private void EnsureIbaColumn(IbaTaskView v)
    {
        if (v.Column != null) return;
        var body = new StackPanel();
        var header = new TextBlock
        {
            FontSize = 11, FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#444444"), Margin = new Thickness(0, 0, 0, 2),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        if (v.TaskTitle.Length > 0) header.Text = v.TaskTitle;
        var obj = new TextBlock
        {
            FontSize = 10, Foreground = Brush("#999999"), TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 4), MaxHeight = 34
        };
        var inner = new StackPanel();
        inner.Children.Add(header); inner.Children.Add(obj); inner.Children.Add(body);
        v.Column = new Border
        {
            BorderBrush = Brush("#BFD3E6"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF)),
            Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(16, 2, 0, 4),
            HorizontalAlignment = HorizontalAlignment.Stretch, Child = inner
        };
        v.Body = body; v.Header = header; v.ObjectiveLine = obj;
        MessagesPanel.Children.Add(v.Column);
    }

    private bool IbaViewActive(IbaTaskView v)
        => !v.Completed && !v.Waiting && (v.HasPendingTool || DateTime.Now - v.LastActivity < IbaParallelWindow);

    private void SyncIbaLayout(IbaTaskView current)
    {
        int active = _ibaViews.Values.Count(IbaViewActive);
        if (active >= 2)
        {
            EnsureIbaHost();
            foreach (var v in _ibaViews.Values) if (IbaViewActive(v)) MoveIbaIntoHost(v);
            ResizeIbaColumns();
        }
        else if (current.Column != null && current.Column.Parent == _ibaHost)
        {
            MessagesPanel.Children.Remove(current.Column);
            MessagesPanel.Children.Add(current.Column);
            current.Column.HorizontalAlignment = HorizontalAlignment.Stretch;
            current.Column.ClearValue(WidthProperty);
            current.Column.ClearValue(MinWidthProperty);
            current.Column.ClearValue(MaxWidthProperty);
            if (_ibaHost != null && _ibaHost.Children.Count == 0)
            {
                MessagesPanel.Children.Remove(_ibaHost);
                _ibaHost = null;
            }
        }
    }

    private void EnsureIbaHost()
    {
        if (_ibaHost != null) return;
        _ibaHost = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
        _ibaHost.SizeChanged += (_, _) => ResizeIbaColumns();
        MessagesPanel.Children.Add(_ibaHost);
    }

    private void MoveIbaIntoHost(IbaTaskView v)
    {
        if (v.Column == null || v.Column.Parent == _ibaHost) return;
        if (v.Column.Parent is Panel p) p.Children.Remove(v.Column);
        v.Column.Margin = new Thickness(0, 0, 10, 4);
        v.Column.HorizontalAlignment = HorizontalAlignment.Left;
        v.Column.MinWidth = 280; v.Column.MaxWidth = 480;
        _ibaHost!.Children.Add(v.Column);
    }

    private void ResizeIbaColumns()
    {
        if (_ibaHost == null || _ibaHost.Children.Count == 0) return;
        double avail = MessagesPanel.ActualWidth > 0 ? MessagesPanel.ActualWidth - 40 : 560;
        double w = Math.Max(280, avail / _ibaHost.Children.Count - 12);
        foreach (UIElement c in _ibaHost.Children)
            if (c is FrameworkElement fe) fe.Width = w;
    }

    private static SolidColorBrush Brush(string hex)
    {
        var c = (Color)ColorConverter.ConvertFromString(hex);
        return new SolidColorBrush(c);
    }
}
