using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DoryNative.Models;

// Server status
public class ServerStatusResponse
{
    public string Status { get; set; } = "UNKNOWN";
    public string Port { get; set; } = "";
    public string? ApiKey { get; set; }
    public string? Error { get; set; }
}

// Activity logs
public class ActivityResponse
{
    public List<LogEntry> Logs { get; set; } = new();
    public bool IsBusy { get; set; }
    public string StatusLabel { get; set; } = "";
    public int LatestId { get; set; }
}

public class LogEntry
{
    public int Id { get; set; }
    public string Timestamp { get; set; } = "";
    public string Level { get; set; } = "";
    public string Message { get; set; } = "";
}

// IBA sessions
public class SessionsResponse
{
    public List<SessionDto> Sessions { get; set; } = new();
    [System.Text.Json.Serialization.JsonPropertyName("active_session")]
    public string ActiveSession { get; set; } = "default";
}

public class SessionDto
{
    [System.Text.Json.Serialization.JsonPropertyName("session_id")]
    public string SessionId { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("persona")]
    public PersonaDto? Persona { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("is_active")]
    public bool IsActive { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("last_active")]
    public double LastActive { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("task_count")]
    public int TaskCount { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("first_prompt")]
    public string FirstPrompt { get; set; } = "";
}

public class PersonaDto
{
    public string Name { get; set; } = "";
    public string Bio { get; set; } = "";
    public string Avatar { get; set; } = "";
    public string Banner { get; set; } = "";
}

// Session history — matches /api/iba/sessions/{id}/history
// which returns {"workspace","session","messages":[...]}
public class SessionHistoryResponse
{
    public string Workspace { get; set; } = "";
    public string Session { get; set; } = "";
    public List<IbaHistoryMessage> Messages { get; set; } = new();
}

public class IbaHistoryMessage
{
    [System.Text.Json.Serialization.JsonPropertyName("role")]
    public string Role { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("content")]
    public string Content { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("reasoning")]
    public string Reasoning { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("task_id")]
    public string TaskId { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("task_title")]
    public string TaskTitle { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("status")]
    public string Status { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("reason")]
    public string Reason { get; set; } = "";
    // Backend emits a unix float here; keep it untyped so deserialization
    // doesn't fail the whole history payload.
    [System.Text.Json.Serialization.JsonPropertyName("timestamp")]
    public object? Timestamp { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("step_groups")]
    public List<IbaStepGroup> StepGroups { get; set; } = new();
    [System.Text.Json.Serialization.JsonPropertyName("steps")]
    public List<IbaHistoryStep> Steps { get; set; } = new();
}

public class IbaStepGroup
{
    [System.Text.Json.Serialization.JsonPropertyName("task_id")]
    public string TaskId { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("title")]
    public string Title { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("steps")]
    public List<IbaHistoryStep> Steps { get; set; } = new();
}

public class IbaHistoryStep
{
    [System.Text.Json.Serialization.JsonPropertyName("step_id")]
    public string StepId { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("step_number")]
    public int StepNumber { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("step_title")]
    public string StepTitle { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("task_id")]
    public string TaskId { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("action_name")]
    public string ActionName { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("action_args")]
    public Dictionary<string, object>? ActionArgs { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("action_state")]
    public string ActionState { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("action_result_body")]
    public string ActionResultBody { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("reasoning")]
    public string Reasoning { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("input_tokens")]
    public int? InputTokens { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("output_tokens")]
    public int? OutputTokens { get; set; }
}

// Provider — /api/iba/provider returns { config:{provider,lm_model}, effective,
// dory_engine, lmstudio_loaded }; convenience members flatten it for callers.
public class ProviderResponse
{
    public ProviderConfigInfo? Config { get; set; }
    public string? Effective { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("dory_engine")]
    public string? DoryEngine { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("lmstudio_loaded")]
    public List<string> LoadedModels { get; set; } = new();

    public string Provider => Config?.Provider ?? Effective ?? "auto";
    public string? LmModel => Config?.LmModel;
    public bool DoryRunning => string.Equals(DoryEngine, "RUNNING", StringComparison.OrdinalIgnoreCase);
    public List<string> LmStudioLoaded => LoadedModels;
}

public class ProviderConfigInfo
{
    public string? Provider { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("lm_model")]
    public string? LmModel { get; set; }
}

// Workspaces
public class WorkspacesResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("active_workspace")]
    public string ActiveWorkspace { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("active_session")]
    public string ActiveSession { get; set; } = "";
    public string Owner { get; set; } = "";
    public List<WorkspaceInfo> Workspaces { get; set; } = new();
}

public class WorkspaceInfo
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string Description { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("created_at")]
    public double CreatedAt { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("is_active")]
    public bool IsActive { get; set; }
    public WorkspaceStats? Stats { get; set; }
}

public class WorkspaceStats
{
    public bool Exists { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("db_initialized")]
    public bool DbInitialized { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("tasks_count")]
    public int TasksCount { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("steps_count")]
    public int StepsCount { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("sessions_count")]
    public int SessionsCount { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("db_size_kb")]
    public double DbSizeKb { get; set; }
}

// Chat streaming
public class ChatStreamRequest
{
    public List<ChatMessage> Messages { get; set; } = new();
    [JsonPropertyName("use_iba")] public bool UseIba { get; set; } = true;
    [JsonPropertyName("workspace_path")] public string? WorkspacePath { get; set; }
    [JsonPropertyName("session_id")] public string? SessionId { get; set; }
    public double Temperature { get; set; } = 0.7;
    [JsonPropertyName("top_p")] public double TopP { get; set; } = 0.9;
    [JsonPropertyName("max_tokens")] public int MaxTokens { get; set; } = 8192;
    [JsonPropertyName("variable_newconv")] public bool VariableNewconv { get; set; } = true;
    [JsonPropertyName("reasoning_effort")] public double? ReasoningEffort { get; set; }
    public List<string>? Images { get; set; }
}

public class ChatMessage
{
    public string Role { get; set; } = "";
    public object Content { get; set; } = "";
}

public class ChatStreamEvent
{
    public string Type { get; set; } = "";
    public string? Text { get; set; }
    public object? Thought { get; set; }
    public object? Step { get; set; }
    public object? Task { get; set; }
    public List<int>? TaskWaiting { get; set; }
    public bool Done { get; set; }
    public string? Error { get; set; }
}

// SSE event: named event + raw JSON payload
public class SseEvent
{
    public string Name { get; set; } = "";
    public System.Text.Json.JsonElement Data { get; set; }
}

// IBA Tasks — /api/iba/tasks returns {"workspace","session","tasks":[...]}
public class TasksResponse
{
    public string Workspace { get; set; } = "";
    public string Session { get; set; } = "";
    public List<IbaTask> Tasks { get; set; } = new();
}

public class IbaTask
{
    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public string Id { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("objective")]
    public string Objective { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("objective_preview")]
    public string ObjectivePreview { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("title")]
    public string Title { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("status")]
    public string Status { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("reason")]
    public string? Reason { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("created")]
    public string CreatedAt { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("parent")]
    public string? Parent { get; set; }
}

// SQL Authority
public class SqlDatabase
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public int TableCount { get; set; }
    public long SizeBytes { get; set; }
    public List<string> Tables { get; set; } = new();
}

public class SqlGraph
{
    public List<SqlNode> Nodes { get; set; } = new();
    public List<SqlEdge> Edges { get; set; } = new();
    public Dictionary<string, object>? Meta { get; set; }
}

public class SqlNode
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Type { get; set; } = "";
    public Dictionary<string, object>? Properties { get; set; }
}

public class SqlEdge
{
    public string Source { get; set; } = "";
    public string Target { get; set; } = "";
    public string Label { get; set; } = "";
}

// Sandbox
public class SandboxStatus
{
    public bool Enabled { get; set; }
    public string? ContainerId { get; set; }
    public string? Image { get; set; }
    public string Status { get; set; } = "unknown";
    public Dictionary<string, object>? Policy { get; set; }
    public List<string> MountedPaths { get; set; } = new();
    public Dictionary<string, JsonElementShim>? Stats { get; set; }
}

// JsonElement shim for flexible dict values
public class JsonElementShim
{
    public string? Value { get; set; }
}
