namespace ShoMark.Application.Common;

/// <summary>Typed SSE message used by the in-process notification bus.</summary>
public record SseEvent(string EventType, string Data);
