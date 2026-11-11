using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FWO.Logging;

namespace FWO.Services.RulePositioning;

/// <summary>Writes safe, correlated diagnostics for one rule-positioning attempt.</summary>
public static class RulePositioningDiagnosticLog
{
    private const int kSchemaVersion = 1;
    private const int kHashPrefixLength = 16;
    private const string kLogTitle = "Rule positioning diagnostic";
    private const string kPrefix = "RPDIAG ";

    private static readonly AsyncLocal<AttemptState?> kCurrentAttempt = new();
    private static readonly JsonSerializerOptions kJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <summary>Begins an attempt whose context flows through asynchronous calls.</summary>
    public static IDisposable BeginAttempt(
        long implementationTaskId,
        long requestTaskId,
        long ticketId,
        int gatewayId)
    {
        AttemptState? previousAttempt = kCurrentAttempt.Value;
        AttemptState attempt = new(
            Guid.NewGuid(),
            implementationTaskId,
            requestTaskId,
            ticketId,
            gatewayId);
        kCurrentAttempt.Value = attempt;
        return new AttemptScope(attempt, previousAttempt);
    }

    /// <summary>Writes an informational diagnostic event when an attempt is active.</summary>
    public static void WriteInfo(
        string stage,
        string outcome,
        IReadOnlyDictionary<string, object?>? facts = null)
    {
        Write(stage, outcome, facts, DiagnosticSeverity.Info);
    }

    /// <summary>Writes a warning diagnostic for an expected positioning failure.</summary>
    public static void WriteFailure(
        string stage,
        IReadOnlyDictionary<string, object?>? facts = null)
    {
        Write(stage, "failure", facts, DiagnosticSeverity.Warning);
    }

    /// <summary>Writes exception types and stack information without exception messages.</summary>
    public static void WriteException(
        string stage,
        Exception exception,
        IReadOnlyDictionary<string, object?>? facts = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Dictionary<string, object?> safeFacts = facts == null
            ? []
            : new Dictionary<string, object?>(facts);
        safeFacts["exception_type"] = exception.GetType().FullName ?? exception.GetType().Name;
        safeFacts["inner_exception_types"] = InnerExceptionTypes(exception);
        safeFacts["stack_trace"] = exception.StackTrace ?? string.Empty;
        Write(stage, "exception", safeFacts, DiagnosticSeverity.Error);
    }

    /// <summary>Creates a deterministic, bounded identity without retaining the supplied text.</summary>
    public static RulePositioningDiagnosticFingerprint Fingerprint(string? value)
    {
        string nonNullValue = value ?? string.Empty;
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(nonNullValue));
        string hashPrefix = Convert.ToHexString(hash)[..kHashPrefixLength].ToLowerInvariant();
        return new RulePositioningDiagnosticFingerprint(nonNullValue.Length, hashPrefix);
    }

    /// <summary>Builds and emits one compact event for the current attempt.</summary>
    private static void Write(
        string stage,
        string outcome,
        IReadOnlyDictionary<string, object?>? facts,
        DiagnosticSeverity severity)
    {
        AttemptState? attempt = kCurrentAttempt.Value;
        if (attempt == null)
        {
            return;
        }

        DiagnosticEvent diagnosticEvent = new(
            kSchemaVersion,
            attempt.AttemptId,
            Interlocked.Increment(ref attempt.Sequence),
            attempt.Stopwatch.ElapsedMilliseconds,
            Environment.ProcessId,
            attempt.ImplementationTaskId,
            attempt.RequestTaskId,
            attempt.TicketId,
            attempt.GatewayId,
            stage,
            NormalizeOutcome(outcome),
            facts ?? EmptyFacts.Instance);
        string text = kPrefix + JsonSerializer.Serialize(diagnosticEvent, kJsonOptions);

        switch (severity)
        {
            case DiagnosticSeverity.Info:
                Log.WriteInfo(kLogTitle, text);
                break;
            case DiagnosticSeverity.Warning:
                Log.WriteWarning(kLogTitle, text);
                break;
            case DiagnosticSeverity.Error:
                Log.WriteError(kLogTitle, text);
                break;
        }
    }

    /// <summary>Keeps the event contract on its fixed outcome vocabulary.</summary>
    private static string NormalizeOutcome(string outcome) => outcome switch
    {
        "start" or "success" or "failure" or "exception" or "skipped" or "summary" => outcome,
        _ => "summary"
    };

    /// <summary>Returns the ordered inner-exception type chain.</summary>
    private static List<string> InnerExceptionTypes(Exception exception)
    {
        List<string> types = [];
        Exception? current = exception.InnerException;
        while (current != null)
        {
            types.Add(current.GetType().FullName ?? current.GetType().Name);
            current = current.InnerException;
        }
        return types;
    }

    private sealed class AttemptScope(AttemptState attempt, AttemptState? previousAttempt) : IDisposable
    {
        private bool disposed;

        /// <inheritdoc />
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            if (ReferenceEquals(kCurrentAttempt.Value, attempt))
            {
                kCurrentAttempt.Value = previousAttempt;
            }
        }
    }

    private sealed class AttemptState(
        Guid attemptId,
        long implementationTaskId,
        long requestTaskId,
        long ticketId,
        int gatewayId)
    {
        public Guid AttemptId { get; } = attemptId;
        public long ImplementationTaskId { get; } = implementationTaskId;
        public long RequestTaskId { get; } = requestTaskId;
        public long TicketId { get; } = ticketId;
        public int GatewayId { get; } = gatewayId;
        public Stopwatch Stopwatch { get; } = Stopwatch.StartNew();
        public long Sequence;
    }

    private sealed record DiagnosticEvent(
        int SchemaVersion,
        Guid AttemptId,
        long Sequence,
        long ElapsedMs,
        int ProcessId,
        long ImplementationTaskId,
        long RequestTaskId,
        long TicketId,
        int GatewayId,
        string Stage,
        string Outcome,
        IReadOnlyDictionary<string, object?> Facts);

    private sealed class EmptyFacts : Dictionary<string, object?>
    {
        public static EmptyFacts Instance { get; } = [];
    }

    private enum DiagnosticSeverity
    {
        Info,
        Warning,
        Error
    }
}

/// <summary>Safe representation of a customer-derived string identity.</summary>
public sealed record RulePositioningDiagnosticFingerprint(int Length, string Sha256Prefix);
