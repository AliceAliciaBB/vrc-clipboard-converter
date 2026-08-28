namespace VrcClipboardConverter.Models;

public record HistoryEntry(
    DateTime Timestamp,
    string OriginalUrl,
    string? DirectUrl,
    bool Success,
    string? ErrorSummary);
