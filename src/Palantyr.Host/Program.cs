using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Palantyr.AI.Orchestration;
using Palantyr.Audio;
using Palantyr.Contracts;
using Palantyr.Data.Raven;
using Palantyr.Domain;
using Palantyr.Host;

var builder = WebApplication.CreateBuilder(args);

// Strictly bind local loopback only (127.0.0.1) to enforce NFR-PRIV-005
builder.WebHost.UseUrls("http://127.0.0.1:5179");

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddSingleton(RavenEmbeddedConfiguration.Default);
builder.Services.AddSingleton<ICampaignMemoryStore, RavenCampaignMemoryStore>();
builder.Services.AddSingleton<ISystemStore, RavenSystemStore>();
builder.Services.AddSingleton<IRuntimePriorityScheduler, RuntimePriorityScheduler>();
builder.Services.AddSingleton<ISystemAssessmentService, WindowsSystemAssessmentService>();
builder.Services.AddSingleton<IAudioRecordingEngine, AudioRecordingEngine>();
builder.Services.AddSingleton<WindowCloseGuardService>();
builder.Services.AddSingleton<ITranscriptionEngine, LocalWhisperTranscriptionEngine>();
builder.Services.AddSingleton<IContextClassifier, TwoStageContextClassifier>();
builder.Services.AddSingleton<ITranscriptionQueue, PersistentTranscriptionQueue>();
builder.Services.AddSingleton<CampaignMemoryService>();

var app = builder.Build();

var api = app.MapGroup("/api/v1");

// ============================================================================
// System Assessment & Microphone Validation (EPIC-01)
// ============================================================================
api.MapGet("/system/assessment", async (
    ISystemStore systemStore,
    ISystemAssessmentService assessmentService,
    CancellationToken ct) =>
{
    var latest = await systemStore.GetLatestAssessmentAsync(ct);
    if (latest is null)
    {
        latest = await assessmentService.RunAssessmentAsync(includeBenchmarkProbe: true, ct);
        await systemStore.SaveAssessmentAsync(latest, ct);
    }
    return Results.Ok(latest);
});

api.MapPost("/system/assessment/run", async (
    ISystemAssessmentService assessmentService,
    ISystemStore systemStore,
    CancellationToken ct) =>
{
    var profile = await assessmentService.RunAssessmentAsync(includeBenchmarkProbe: true, ct);
    await systemStore.SaveAssessmentAsync(profile, ct);
    return Results.Ok(profile);
});

api.MapPost("/system/microphone/validate", async (
    [FromBody] ValidateMicrophoneRequest? request,
    ISystemAssessmentService assessmentService,
    CancellationToken ct) =>
{
    var result = await assessmentService.ValidateMicrophoneAsync(request?.DeviceId, ct);
    return Results.Ok(result);
});

// ============================================================================
// Global & Campaign Settings (Section 38)
// ============================================================================
api.MapGet("/settings", async (ISystemStore systemStore, CancellationToken ct) =>
{
    var settings = await systemStore.GetSettingsAsync(ct);
    return Results.Ok(settings);
});

api.MapPut("/settings", async (
    [FromBody] AppSettingsDto settings,
    ISystemStore systemStore,
    CancellationToken ct) =>
{
    var saved = await systemStore.SaveSettingsAsync(settings, ct);
    return Results.Ok(saved);
});

// ============================================================================
// Campaigns, Party Roster & Safe Deletion (EPIC-02, EPIC-11)
// ============================================================================
api.MapGet("/campaigns", async (ISystemStore systemStore, CancellationToken ct) =>
{
    var campaigns = await systemStore.ListCampaignsAsync(ct);
    return Results.Ok(campaigns);
});

api.MapPost("/campaigns", async (
    [FromBody] CreateCampaignRequest request,
    ISystemStore systemStore,
    CancellationToken ct) =>
{
    var created = await systemStore.CreateCampaignAsync(request, ct);
    return Results.Created($"/api/v1/campaigns/{created.Id}", created);
});

api.MapGet("/campaigns/{id}", async (
    string id,
    ISystemStore systemStore,
    CancellationToken ct) =>
{
    var campaign = await systemStore.GetCampaignAsync(id, ct);
    return campaign is not null ? Results.Ok(campaign) : Results.NotFound();
});

api.MapPut("/campaigns/{id}/roster", async (
    string id,
    [FromBody] IReadOnlyList<PartyMemberDto> roster,
    ISystemStore systemStore,
    CancellationToken ct) =>
{
    var updated = await systemStore.UpdatePartyRosterAsync(id, roster, ct);
    return Results.Ok(updated);
});

api.MapPost("/campaigns/{id}/delete", async (
    string id,
    [FromBody] DeleteCampaignRequest request,
    ISystemStore systemStore,
    RavenEmbeddedConfiguration ravenConfig,
    CancellationToken ct) =>
{
    string backupDir = string.IsNullOrWhiteSpace(request.BackupDirectory)
        ? ravenConfig.GetBackupsDirectoryPath()
        : request.BackupDirectory;

    var backupPath = await systemStore.DeleteCampaignSafelyAsync(id, request.Option, backupDir, ct);
    return Results.Ok(new DeleteCampaignResponse(
        CampaignId: id,
        Option: request.Option,
        Deleted: request.Option != CampaignDeletionOption.Cancel,
        BackupFilePath: backupPath));
});

// ============================================================================
// Live Session Recording Lifecycle & Recovery (EPIC-03)
// ============================================================================
api.MapGet("/campaigns/{id}/status", async (
    string id,
    IAudioRecordingEngine recordingEngine,
    CancellationToken ct) =>
{
    var status = await recordingEngine.GetLiveStatusAsync(id, ct);
    return Results.Ok(status);
});

api.MapPost("/campaigns/{id}/sessions/start", async (
    string id,
    [FromBody] StartSessionRequest? request,
    IAudioRecordingEngine recordingEngine,
    WindowCloseGuardService closeGuard,
    CancellationToken ct) =>
{
    string sessionId = string.IsNullOrWhiteSpace(request?.SessionId)
        ? $"session-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}"
        : request.SessionId;

    var status = await recordingEngine.StartSessionRecordingAsync(id, sessionId, request?.MicrophoneDeviceId, ct);
    closeGuard.NotifySessionState(id, sessionId, status.SessionState);
    return Results.Ok(status);
});

api.MapPost("/campaigns/{id}/sessions/{sessionId}/pause", async (
    string id,
    string sessionId,
    IAudioRecordingEngine recordingEngine,
    ITranscriptionQueue transcriptionQueue,
    ISystemStore systemStore,
    WindowCloseGuardService closeGuard,
    CancellationToken ct) =>
{
    var status = await recordingEngine.PauseSessionRecordingAsync(id, sessionId, ct);
    closeGuard.NotifySessionState(id, sessionId, status.SessionState);

    var settings = await systemStore.GetSettingsAsync(ct);
    if (settings.PausePreference == PauseTranscriptionBehavior.Always)
    {
        _ = transcriptionQueue.ProcessPauseCatchUpAsync(id, sessionId, settings.PausePreference, CancellationToken.None);
    }

    return Results.Ok(status);
});

api.MapPost("/campaigns/{id}/sessions/{sessionId}/resume", async (
    string id,
    string sessionId,
    IAudioRecordingEngine recordingEngine,
    WindowCloseGuardService closeGuard,
    CancellationToken ct) =>
{
    var status = await recordingEngine.ResumeSessionRecordingAsync(id, sessionId, ct);
    closeGuard.NotifySessionState(id, sessionId, status.SessionState);
    return Results.Ok(status);
});

api.MapPost("/campaigns/{id}/sessions/{sessionId}/end", async (
    string id,
    string sessionId,
    IAudioRecordingEngine recordingEngine,
    WindowCloseGuardService closeGuard,
    CancellationToken ct) =>
{
    var summary = await recordingEngine.EndSessionRecordingAsync(id, sessionId, ct);
    closeGuard.NotifySessionState(id, sessionId, SessionRecordingState.Ended);
    return Results.Ok(summary);
});

api.MapGet("/campaigns/{id}/recovery", async (
    string id,
    IAudioRecordingEngine recordingEngine,
    CancellationToken ct) =>
{
    var reports = await recordingEngine.ScanAndRecoverInterruptedSessionsAsync(id, ct);
    return Results.Ok(reports);
});

api.MapPost("/campaigns/{id}/recovery/{sessionId}/resolve", async (
    string id,
    string sessionId,
    [FromBody] ResolveRecoveryRequest request,
    IAudioRecordingEngine recordingEngine,
    WindowCloseGuardService closeGuard,
    CancellationToken ct) =>
{
    var status = await recordingEngine.ResolveRecoveredSessionAsync(id, sessionId, request.Choice, ct);
    if (!string.IsNullOrWhiteSpace(status.ActiveSessionId))
    {
        closeGuard.NotifySessionState(id, status.ActiveSessionId, status.SessionState);
    }
    else
    {
        closeGuard.NotifySessionState(id, sessionId, status.SessionState);
    }
    return Results.Ok(status);
});

// ============================================================================
// Campaign Memory & Pre-Generation Visibility Gatekeeper (EPIC-06, EPIC-08)
// ============================================================================
api.MapGet("/campaigns/{id}/memory/party-safe", async (
    string id,
    [FromQuery] MemoryEntityType? filterType,
    ICampaignMemoryStore memoryStore,
    CancellationToken ct) =>
{
    var views = await memoryStore.QueryPartySafeMemoryAsync(id, QueryVisibilityContext.PartySafe, filterType, ct);
    return Results.Ok(views);
});

api.MapGet("/campaigns/{id}/memory/dm-full", async (
    string id,
    [FromQuery] MemoryEntityType? filterType,
    ICampaignMemoryStore memoryStore,
    CancellationToken ct) =>
{
    var entities = await memoryStore.QueryDmFullMemoryAsync(id, filterType, ct);
    return Results.Ok(entities);
});


// ============================================================================
// Wave 2: Transcription Pipeline & Context Classification (EPIC-04, EPIC-05)
// ============================================================================
api.MapPost("/campaigns/{id}/sessions/{sessionId}/transcription/enqueue", (
    string id,
    string sessionId,
    [FromBody] EnqueueTranscriptionRequest request,
    ITranscriptionQueue queue) =>
{
    queue.EnqueueSegment(new TranscriptionJobRequest(
        CampaignId: id,
        SessionId: sessionId,
        SegmentId: request.SegmentId,
        AudioFilePath: request.AudioFilePath,
        Language: request.Language,
        Priority: request.Priority,
        FantasyGlossary: request.FantasyGlossary));
    return Results.Accepted(value: new { Enqueued = true, request.SegmentId });
});

api.MapPost("/campaigns/{id}/sessions/{sessionId}/transcription/process-next", async (
    string id,
    string sessionId,
    ITranscriptionQueue queue,
    CancellationToken ct) =>
{
    var result = await queue.ProcessNextJobAsync(ct);
    return result is not null ? Results.Ok(result) : Results.NoContent();
});

api.MapGet("/campaigns/{id}/sessions/{sessionId}/transcription/queue", (
    string id,
    string sessionId,
    ITranscriptionQueue queue) =>
{
    var status = queue.GetQueueStatus(id, sessionId);
    return Results.Ok(status);
});

api.MapPost("/campaigns/{id}/sessions/{sessionId}/transcription/pause-catchup", async (
    string id,
    string sessionId,
    ITranscriptionQueue queue,
    ISystemStore systemStore,
    [FromQuery] PauseTranscriptionBehavior? behavior,
    CancellationToken ct) =>
{
    var effectiveBehavior = behavior;
    if (!effectiveBehavior.HasValue)
    {
        var settings = await systemStore.GetSettingsAsync(ct);
        effectiveBehavior = settings.PausePreference;
    }

    var processed = await queue.ProcessPauseCatchUpAsync(id, sessionId, effectiveBehavior.Value, ct);
    return Results.Ok(new { ProcessedCount = processed, Behavior = effectiveBehavior.Value.ToString() });
});

api.MapGet("/campaigns/{id}/sessions/{sessionId}/transcripts", async (
    string id,
    string sessionId,
    ICampaignMemoryStore memoryStore,
    CancellationToken ct) =>
{
    var segments = await memoryStore.ListTranscriptSegmentsAsync(id, sessionId, ct);
    return Results.Ok(segments);
});

api.MapGet("/campaigns/{id}/sessions/{sessionId}/transcripts/{segmentId}", async (
    string id,
    string sessionId,
    string segmentId,
    ICampaignMemoryStore memoryStore,
    CancellationToken ct) =>
{
    var segment = await memoryStore.GetTranscriptSegmentAsync(id, sessionId, segmentId, ct);
    return segment is not null ? Results.Ok(segment) : Results.NotFound();
});

api.MapPost("/campaigns/{id}/sessions/{sessionId}/transcripts/{segmentId}/correct", async (
    string id,
    string sessionId,
    string segmentId,
    [FromBody] CorrectTranscriptSpanRequest request,
    ICampaignMemoryStore memoryStore,
    CancellationToken ct) =>
{
    var segment = await memoryStore.GetTranscriptSegmentAsync(id, sessionId, segmentId, ct);
    if (segment is null) return Results.NotFound();

    segment.ApplyCorrection(request.SpanId, request.NewCorrectedText, request.CorrectedBy ?? "DM");
    await memoryStore.SaveTranscriptSegmentAsync(id, segment, ct);
    return Results.Ok(segment);
});

api.MapPost("/campaigns/{id}/sessions/{sessionId}/transcripts/{segmentId}/promote-joke", async (
    string id,
    string sessionId,
    string segmentId,
    [FromBody] PromoteJokeRequest request,
    ICampaignMemoryStore memoryStore,
    CancellationToken ct) =>
{
    var segment = await memoryStore.GetTranscriptSegmentAsync(id, sessionId, segmentId, ct);
    if (segment is null) return Results.NotFound();

    segment.PromoteJokeToCanon(request.SpanId, request.PromotionReason, request.ConfirmedBy ?? "DM");
    await memoryStore.SaveTranscriptSegmentAsync(id, segment, ct);
    return Results.Ok(segment);
});

api.MapGet("/campaigns/{id}/needs-attention", async (
    string id,
    [FromQuery] string? sessionId,
    [FromQuery] bool? unresolvedOnly,
    ICampaignMemoryStore memoryStore,
    CancellationToken ct) =>
{
    var items = await memoryStore.ListNeedsAttentionItemsAsync(id, sessionId, unresolvedOnly ?? true, ct);
    return Results.Ok(items);
});

api.MapPost("/campaigns/{id}/needs-attention/{itemId}/resolve", async (
    string id,
    string itemId,
    [FromBody] ResolveNeedsAttentionRequest request,
    ICampaignMemoryStore memoryStore,
    CancellationToken ct) =>
{
    await memoryStore.ResolveNeedsAttentionItemAsync(id, itemId, request.ResolutionDecision, ct);
    return Results.Ok(new { ItemId = itemId, Resolved = true, request.ResolutionDecision });
});

// ============================================================================
// ============================================================================
// Wave 3A: Core Campaign Memory Graph, Resolution & Conflicts (EPIC-06)
// ============================================================================
api.MapGet("/campaigns/{id}/memory", async (
    string id,
    [FromQuery] MemoryEntityType? filterType,
    [FromQuery] string? searchQuery,
    [FromQuery] string? scope,
    ICampaignMemoryStore memoryStore,
    CancellationToken ct) =>
{
    if (string.Equals(scope, "partySafe", StringComparison.OrdinalIgnoreCase))
    {
        var safe = await memoryStore.QueryPartySafeMemoryAsync(id, QueryVisibilityContext.PartySafe, filterType, ct);
        return Results.Ok(safe);
    }

    var list = await memoryStore.ListMemoryEntitiesAsync(id, filterType, searchQuery, ct);
    return Results.Ok(list);
});

api.MapGet("/campaigns/{id}/memory/{entityId}", async (
    string id,
    string entityId,
    ICampaignMemoryStore memoryStore,
    CancellationToken ct) =>
{
    var entity = await memoryStore.GetMemoryEntityAsync(id, entityId, ct);
    return entity is not null ? Results.Ok(entity) : Results.NotFound();
});


api.MapPut("/campaigns/{id}/memory/{entityId}", async (
    string id,
    string entityId,
    [FromBody] UpdateMemoryEntityRequest request,
    ICampaignMemoryStore memoryStore,
    CancellationToken ct) =>
{
    var entity = await memoryStore.GetMemoryEntityAsync(id, entityId, ct);
    if (entity is null) return Results.NotFound();

    if (!string.IsNullOrWhiteSpace(request.CanonicalName)) entity.CanonicalName = request.CanonicalName.Trim();
    if (request.Aliases is not null) { entity.Aliases.Clear(); entity.Aliases.AddRange(request.Aliases); }
    if (request.Titles is not null) { entity.Titles.Clear(); entity.Titles.AddRange(request.Titles); }
    if (request.RecognitionVariants is not null) { entity.RecognitionVariants.Clear(); entity.RecognitionVariants.AddRange(request.RecognitionVariants); }
    if (request.SummaryDescription is not null) entity.SummaryDescription = request.SummaryDescription;
    if (request.DmPrivateNotes is not null) entity.DmPrivateNotes = request.DmPrivateNotes;
    if (request.Visibility.HasValue) entity.Visibility = request.Visibility.Value;
    if (request.KnowledgeState.HasValue) entity.KnowledgeState = request.KnowledgeState.Value;
    if (request.Confidence.HasValue) entity.Confidence = request.Confidence.Value;

    await memoryStore.SaveMemoryEntityAsync(id, entity, ct);
    return Results.Ok(entity);
});

api.MapDelete("/campaigns/{id}/memory/{entityId}", async (
    string id,
    string entityId,
    ICampaignMemoryStore memoryStore,
    CancellationToken ct) =>
{
    await memoryStore.DeleteMemoryEntityAsync(id, entityId, ct);
    return Results.NoContent();
});

api.MapGet("/campaigns/{id}/memory/resolution/candidates", async (
    string id,
    [FromQuery] EntityResolutionStatus? statusFilter,
    ICampaignMemoryStore memoryStore,
    CancellationToken ct) =>
{
    var candidates = await memoryStore.ListResolutionCandidatesAsync(id, statusFilter, ct);
    return Results.Ok(candidates);
});

api.MapPost("/campaigns/{id}/memory/resolution/candidates/{candidateId}/resolve", async (
    string id,
    string candidateId,
    [FromBody] ResolveCandidateRequest request,
    ICampaignMemoryStore memoryStore,
    CampaignMemoryService memoryService,
    CancellationToken ct) =>
{
    var candidate = await memoryStore.GetResolutionCandidateAsync(id, candidateId, ct);
    if (candidate is null) return Results.NotFound();

    var allEntities = await memoryStore.ListMemoryEntitiesAsync(id, null, null, ct);
    var entityMap = allEntities.ToDictionary(e => e.Id, StringComparer.OrdinalIgnoreCase);

    var updated = memoryService.ApplyResolutionRuling(candidate, request, entityId => entityMap.GetValueOrDefault(entityId));
    await memoryStore.SaveResolutionCandidateAsync(id, updated, ct);

    if (updated.Status == EntityResolutionStatus.LinkedToExisting && updated.ResolvedEntityId is not null && entityMap.TryGetValue(updated.ResolvedEntityId, out var linkedEntity))
    {
        await memoryStore.SaveMemoryEntityAsync(id, linkedEntity, ct);
    }
    else if (updated.Status == EntityResolutionStatus.CreatedNewEntity)
    {
        var newEntity = new MemoryEntityDocument
        {
            Id = $"char_{Guid.NewGuid():N}"[..18],
            EntityType = MemoryEntityType.Character,
            CanonicalName = request.NewEntityCanonicalName ?? candidate.ObservedSurfaceName,
            SummaryDescription = $"Created from candidate '{candidate.ObservedSurfaceName}'"
        };
        await memoryStore.SaveMemoryEntityAsync(id, newEntity, ct);
    }

    return Results.Ok(updated);
});

api.MapGet("/campaigns/{id}/memory/conflicts", async (
    string id,
    [FromQuery] string? entityId,
    [FromQuery] bool? unresolvedOnly,
    ICampaignMemoryStore memoryStore,
    CancellationToken ct) =>
{
    var conflicts = await memoryStore.ListConflictsAsync(id, entityId, unresolvedOnly ?? true, ct);
    return Results.Ok(conflicts);
});

api.MapPost("/campaigns/{id}/memory/conflicts/{conflictId}/resolve", async (
    string id,
    string conflictId,
    [FromBody] ResolveConflictRequest request,
    ICampaignMemoryStore memoryStore,
    CancellationToken ct) =>
{
    var conflict = await memoryStore.GetConflictAsync(id, conflictId, ct);
    if (conflict is null) return Results.NotFound();

    var resolvedConflict = conflict with
    {
        State = request.Resolution,
        DmRuling = request.DmRulingNote,
        ResolvedAtUtc = DateTimeOffset.UtcNow
    };

    await memoryStore.SaveConflictAsync(id, resolvedConflict, ct);
    return Results.Ok(resolvedConflict);
});

api.MapGet("/campaigns/{id}/memory/locations/hierarchy", async (
    string id,
    ICampaignMemoryStore memoryStore,
    CancellationToken ct) =>
{
    var tree = await memoryStore.GetLocationHierarchyTreeAsync(id, ct);
    return Results.Ok(tree);
});

// Window Close Guard (NFR-REL-009, AC-REL-004)
// ============================================================================
api.MapPost("/window/close-check", async (
    [FromBody] WindowCloseCheckRequest? request,
    WindowCloseGuardService closeGuard,
    ISystemStore systemStore,
    ICampaignMemoryStore memoryStore,
    IAudioRecordingEngine recordingEngine,
    CancellationToken ct) =>
{
    if (request?.ResolutionAction is not null)
    {
        var resolved = await closeGuard.ResolveCloseDecisionAsync(request.ResolutionAction.Value, recordingEngine, ct);
        return Results.Ok(resolved);
    }

    var check = await closeGuard.EvaluateWindowCloseAsync(systemStore, memoryStore, ct);
    return Results.Ok(check);
});

app.Run();

namespace Palantyr.Host
{
    public sealed record ValidateMicrophoneRequest(string? DeviceId);
    public sealed record StartSessionRequest(string? SessionId, string? MicrophoneDeviceId);
    public sealed record ResolveRecoveryRequest(CrashRecoveryChoice Choice);
    public sealed record DeleteCampaignRequest(CampaignDeletionOption Option, string? BackupDirectory = null);
    public sealed record DeleteCampaignResponse(string CampaignId, CampaignDeletionOption Option, bool Deleted, string? BackupFilePath);
    public sealed record WindowCloseCheckRequest(WindowCloseResolutionAction? ResolutionAction = null);
    public sealed record EnqueueTranscriptionRequest(string SegmentId, string AudioFilePath, PalantyrLanguage Language, int Priority = 0, IReadOnlyList<string>? FantasyGlossary = null);
    public sealed record CorrectTranscriptSpanRequest(string SpanId, string NewCorrectedText, string? CorrectedBy = null);
    public sealed record PromoteJokeRequest(string SpanId, string PromotionReason, string? ConfirmedBy = null);
    public sealed record ResolveNeedsAttentionRequest(string ResolutionDecision);
    public partial class Program;
}
