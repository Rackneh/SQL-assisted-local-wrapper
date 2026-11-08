using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DoryNative.Models;

namespace DoryNative.Services;

public class BackendApi
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly JsonSerializerOptions _jsonOptions;

    public BackendApi(string baseUrl = "http://127.0.0.1:5000")
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_baseUrl),
            Timeout = TimeSpan.FromMinutes(5)
        };
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    // Server status endpoints
    public async Task<ServerStatusResponse> GetServerStatusAsync()
    {
        var response = await _httpClient.GetAsync("/api/server/status");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ServerStatusResponse>(_jsonOptions) ?? new ServerStatusResponse();
    }

    public async Task StartServerAsync()
    {
        var response = await _httpClient.PostAsync("/api/server/start", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task StopServerAsync()
    {
        var response = await _httpClient.PostAsync("/api/server/stop", null);
        response.EnsureSuccessStatusCode();
    }

    // IBA sessions
    public async Task<SessionsResponse> GetSessionsAsync()
    {
        var response = await _httpClient.GetAsync("/api/iba/sessions");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SessionsResponse>(_jsonOptions) ?? new SessionsResponse();
    }

    public async Task SelectSessionAsync(string sessionId)
    {
        var payload = new { session_id = sessionId };
        var content = new StringContent(JsonSerializer.Serialize(payload, _jsonOptions), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("/api/iba/sessions/select", content);
        response.EnsureSuccessStatusCode();
    }

    public async Task<string> CreateSessionAsync(string? sessionId, string? description = null)
    {
        var payload = new { session_id = sessionId, description = description ?? "" };
        var content = new StringContent(JsonSerializer.Serialize(payload, _jsonOptions), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("/api/iba/sessions/create", content);
        response.EnsureSuccessStatusCode();
        try
        {
            var doc = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
            return doc.TryGetProperty("session_id", out var el) ? el.GetString() ?? "" : "";
        }
        catch { return sessionId ?? ""; }
    }

    public async Task DeleteSessionAsync(string sessionId)
    {
        var payload = new { session_id = sessionId };
        var content = new StringContent(JsonSerializer.Serialize(payload, _jsonOptions), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("/api/iba/sessions/delete", content);
        response.EnsureSuccessStatusCode();
    }

    public async Task<SessionHistoryResponse> GetSessionHistoryAsync(string sessionId)
    {
        var response = await _httpClient.GetAsync($"/api/iba/sessions/{sessionId}/history");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SessionHistoryResponse>(_jsonOptions) ?? new SessionHistoryResponse();
    }

    // IBA provider (Dory vs LM Studio)
    public async Task<ProviderResponse> GetProviderAsync()
    {
        var response = await _httpClient.GetAsync("/api/iba/provider");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProviderResponse>(_jsonOptions) ?? new ProviderResponse();
    }

    public async Task SetProviderAsync(string provider, string? lmModel = null, string? lmApiKey = null)
    {
        var payload = new { provider, lm_model = lmModel, lm_api_key = lmApiKey };
        var content = new StringContent(JsonSerializer.Serialize(payload, _jsonOptions), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("/api/iba/provider", content);
        response.EnsureSuccessStatusCode();
    }

    // IBA workspaces
    public async Task<WorkspacesResponse> GetWorkspacesAsync()
    {
        var response = await _httpClient.GetAsync("/api/iba/workspaces");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorkspacesResponse>(_jsonOptions) ?? new WorkspacesResponse();
    }

    public async Task SelectWorkspaceAsync(string workspacePath)
    {
        var payload = new { path = workspacePath };
        var content = new StringContent(JsonSerializer.Serialize(payload, _jsonOptions), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("/api/iba/workspaces/select", content);
        response.EnsureSuccessStatusCode();
    }

    // SSE chat streaming — parses "event: <name>\ndata: <json>" pairs
    public async IAsyncEnumerable<SseEvent> StreamChatAsync(ChatStreamRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/chat/stream")
        {
            Content = content
        };

        using var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new System.IO.StreamReader(stream);

        string? eventName = null;
        var dataLines = new List<string>();

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line == null) break;

            if (line.Length == 0)
            {
                // Dispatch accumulated event
                if (eventName != null && dataLines.Count > 0)
                {
                    var data = string.Join("\n", dataLines);
                    JsonElement payloadEl;
                    try { payloadEl = JsonDocument.Parse(data).RootElement.Clone(); }
                    catch { payloadEl = default; }
                    yield return new SseEvent { Name = eventName, Data = payloadEl };
                }
                eventName = null;
                dataLines.Clear();
                continue;
            }

            if (line.StartsWith(":")) continue; // comment / keepalive

            if (line.StartsWith("event:"))
            {
                eventName = line.Substring(6).Trim();
            }
            else if (line.StartsWith("data:"))
            {
                dataLines.Add(line.Substring(5).TrimStart());
            }
        }

        // Flush trailing event without blank line
        if (eventName != null && dataLines.Count > 0)
        {
            var data = string.Join("\n", dataLines);
            JsonElement payloadEl;
            try { payloadEl = JsonDocument.Parse(data).RootElement.Clone(); }
            catch { payloadEl = default; }
            yield return new SseEvent { Name = eventName, Data = payloadEl };
        }
    }

    // Activity logs
    public async Task<ActivityResponse> GetActivityAsync(int since = 0)
    {
        var response = await _httpClient.GetAsync($"/api/activity?since={since}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ActivityResponse>(_jsonOptions) ?? new ActivityResponse();
    }

    public async Task ClearLogsAsync()
    {
        var response = await _httpClient.PostAsync("/api/logs/clear", null);
        response.EnsureSuccessStatusCode();
    }

    // General status
    public async Task<Dictionary<string, JsonElement>> GetStatusAsync()
    {
        var response = await _httpClient.GetAsync("/api/status");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>(_jsonOptions) ?? new();
    }

    // IBA tasks
    public async Task<List<IbaTask>> GetTasksAsync()
    {
        var response = await _httpClient.GetAsync("/api/iba/tasks");
        response.EnsureSuccessStatusCode();
        var wrapper = await response.Content.ReadFromJsonAsync<TasksResponse>(_jsonOptions);
        return wrapper?.Tasks ?? new List<IbaTask>();
    }

    public async Task<IbaTask?> GetTaskAsync(string taskId)
    {
        var response = await _httpClient.GetAsync($"/api/iba/tasks/{taskId}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<IbaTask>(_jsonOptions);
    }

    // IBA policy
    public async Task<Dictionary<string, JsonElement>> GetPolicyAsync()
    {
        var response = await _httpClient.GetAsync("/api/iba/policy");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>(_jsonOptions) ?? new();
    }

    public async Task SetPolicyAsync(Dictionary<string, object> policy)
    {
        var content = new StringContent(JsonSerializer.Serialize(policy, _jsonOptions), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("/api/iba/policy", content);
        response.EnsureSuccessStatusCode();
    }

    // SQL Authority
    public async Task<List<SqlDatabase>> GetSqlDatabasesAsync()
    {
        var response = await _httpClient.GetAsync("/api/iba/sql/databases");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<SqlDatabase>>(_jsonOptions) ?? new();
    }

    public async Task<SqlGraph> GetSqlGraphAsync()
    {
        var response = await _httpClient.GetAsync("/api/iba/sql/graph");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SqlGraph>(_jsonOptions) ?? new SqlGraph();
    }

    // Sandbox
    public async Task<SandboxStatus> GetSandboxStatusAsync()
    {
        var response = await _httpClient.GetAsync("/api/iba/sandbox/status");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SandboxStatus>(_jsonOptions) ?? new SandboxStatus();
    }

    public async Task SetSandboxPolicyAsync(Dictionary<string, object> policy)
    {
        var content = new StringContent(JsonSerializer.Serialize(policy, _jsonOptions), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("/api/iba/sandbox/policy", content);
        response.EnsureSuccessStatusCode();
    }
}
