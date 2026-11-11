using System.Text.Json;
using FWO.Services.RulePositioning;
using FWO.Test.Helpers;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
[NonParallelizable]
internal class RulePositioningDiagnosticLogTest
{
    private const string kSecretMarker = "CUSTOMER_SECRET_MARKER";
    private const string kSecretHashPrefix = "9b09be13a830afb9";

    [Test]
    public async Task WriteInfo_EmitsReleaseVisibleCorrelatedJson()
    {
        string output = await ConsoleOutput.CaptureAsync(() =>
        {
            using IDisposable attempt = RulePositioningDiagnosticLog.BeginAttempt(101, 202, 303, 404);
            RulePositioningDiagnosticLog.WriteInfo(
                "workflow.start",
                "start",
                new Dictionary<string, object?> { ["applicable"] = true });
            return Task.CompletedTask;
        });

        Assert.That(output, Does.Contain("Info - Rule positioning diagnostic"));
        JsonElement diagnostic = ParseDiagnostics(output).Single();
        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.GetProperty("schema_version").GetInt32(), Is.EqualTo(1));
            Assert.That(Guid.TryParse(diagnostic.GetProperty("attempt_id").GetString(), out _), Is.True);
            Assert.That(diagnostic.GetProperty("sequence").GetInt64(), Is.EqualTo(1));
            Assert.That(diagnostic.GetProperty("elapsed_ms").GetInt64(), Is.GreaterThanOrEqualTo(0));
            Assert.That(diagnostic.GetProperty("process_id").GetInt32(), Is.EqualTo(Environment.ProcessId));
            Assert.That(diagnostic.GetProperty("implementation_task_id").GetInt64(), Is.EqualTo(101));
            Assert.That(diagnostic.GetProperty("request_task_id").GetInt64(), Is.EqualTo(202));
            Assert.That(diagnostic.GetProperty("ticket_id").GetInt64(), Is.EqualTo(303));
            Assert.That(diagnostic.GetProperty("gateway_id").GetInt32(), Is.EqualTo(404));
            Assert.That(diagnostic.GetProperty("stage").GetString(), Is.EqualTo("workflow.start"));
            Assert.That(diagnostic.GetProperty("outcome").GetString(), Is.EqualTo("start"));
            Assert.That(diagnostic.GetProperty("facts").GetProperty("applicable").GetBoolean(), Is.True);
        });
    }

    [Test]
    public async Task ConcurrentAttempts_RetainIndependentIdsAndSequencesAcrossAwait()
    {
        string output = await ConsoleOutput.CaptureAsync(async () =>
        {
            await Task.WhenAll(
                EmitTwoEventsAsync(501, 601),
                EmitTwoEventsAsync(502, 602));
        });

        List<JsonElement> diagnostics = ParseDiagnostics(output);
        Assert.That(diagnostics, Has.Count.EqualTo(4));
        AssertAttempt(diagnostics, 501, 601);
        AssertAttempt(diagnostics, 502, 602);
        Assert.That(
            diagnostics.Select(Event => Event.GetProperty("attempt_id").GetString()).Distinct().Count(),
            Is.EqualTo(2));
    }

    [Test]
    public async Task Dispose_RestoresOuterScopeAndRemovesFinalScope()
    {
        string output = await ConsoleOutput.CaptureAsync(() =>
        {
            using (RulePositioningDiagnosticLog.BeginAttempt(701, 702, 703, 704))
            {
                RulePositioningDiagnosticLog.WriteInfo("outer.before", "summary");
                using (RulePositioningDiagnosticLog.BeginAttempt(801, 802, 803, 804))
                {
                    RulePositioningDiagnosticLog.WriteInfo("inner", "summary");
                }
                RulePositioningDiagnosticLog.WriteInfo("outer.after", "summary");
            }
            RulePositioningDiagnosticLog.WriteInfo("outside", "summary");
            return Task.CompletedTask;
        });

        List<JsonElement> diagnostics = ParseDiagnostics(output);
        Assert.That(diagnostics, Has.Count.EqualTo(3));
        List<JsonElement> outerEvents = diagnostics
            .Where(Event => Event.GetProperty("implementation_task_id").GetInt64() == 701)
            .ToList();
        Assert.That(outerEvents.Select(Event => Event.GetProperty("sequence").GetInt64()), Is.EqualTo(new long[] { 1, 2 }));
        Assert.That(outerEvents.Select(Event => Event.GetProperty("attempt_id").GetString()).Distinct().Count(), Is.EqualTo(1));
        Assert.That(diagnostics.Single(Event => Event.GetProperty("stage").GetString() == "inner")
            .GetProperty("sequence").GetInt64(), Is.EqualTo(1));
        Assert.That(output, Does.Not.Contain("outside"));
    }

    [Test]
    public async Task FingerprintAndException_NeverEmitCustomerTextOrExceptionMessages()
    {
        Exception secretException;
        try
        {
            throw new InvalidOperationException(kSecretMarker, new ArgumentException(kSecretMarker));
        }
        catch (Exception exception)
        {
            secretException = exception;
        }

        string output = await ConsoleOutput.CaptureAsync(() =>
        {
            using IDisposable attempt = RulePositioningDiagnosticLog.BeginAttempt(901, 902, 903, 904);
            RulePositioningDiagnosticLog.WriteException(
                "resolver.exception",
                secretException,
                new Dictionary<string, object?>
                {
                    ["customer_identity"] = RulePositioningDiagnosticLog.Fingerprint(kSecretMarker)
                });
            return Task.CompletedTask;
        });

        Assert.That(output, Does.Not.Contain(kSecretMarker));
        JsonElement facts = ParseDiagnostics(output).Single().GetProperty("facts");
        JsonElement fingerprint = facts.GetProperty("customer_identity");
        Assert.Multiple(() =>
        {
            Assert.That(fingerprint.GetProperty("length").GetInt32(), Is.EqualTo(22));
            Assert.That(fingerprint.GetProperty("sha256_prefix").GetString(), Is.EqualTo(kSecretHashPrefix));
            Assert.That(facts.GetProperty("exception_type").GetString(), Is.EqualTo(typeof(InvalidOperationException).FullName));
            Assert.That(facts.GetProperty("inner_exception_types").EnumerateArray().Select(Value => Value.GetString()),
                Is.EqualTo(new[] { typeof(ArgumentException).FullName }));
            Assert.That(facts.GetProperty("stack_trace").GetString(), Does.Contain(nameof(FingerprintAndException_NeverEmitCustomerTextOrExceptionMessages)));
        });
    }

    [Test]
    public async Task WriteFailure_UsesWarningAndFailureOutcome()
    {
        string output = await ConsoleOutput.CaptureAsync(() =>
        {
            using IDisposable attempt = RulePositioningDiagnosticLog.BeginAttempt(1001, 1002, 1003, 1004);
            RulePositioningDiagnosticLog.WriteFailure("graph.validation");
            return Task.CompletedTask;
        });

        Assert.That(output, Does.Contain("Warning - Rule positioning diagnostic"));
        Assert.That(ParseDiagnostics(output).Single().GetProperty("outcome").GetString(), Is.EqualTo("failure"));
    }

    [Test]
    public async Task WriteInfo_NormalizesUnknownOutcomeToDocumentedVocabulary()
    {
        string output = await ConsoleOutput.CaptureAsync(() =>
        {
            using IDisposable attempt = RulePositioningDiagnosticLog.BeginAttempt(1, 2, 3, 4);
            RulePositioningDiagnosticLog.WriteInfo("test.outcome", "customer-derived-outcome");
            return Task.CompletedTask;
        });

        Assert.That(ParseDiagnostics(output).Single().GetProperty("outcome").GetString(), Is.EqualTo("summary"));
    }

    /// <summary>Emits two events with an asynchronous boundary inside one attempt.</summary>
    private static async Task EmitTwoEventsAsync(long implementationTaskId, long requestTaskId)
    {
        using IDisposable attempt = RulePositioningDiagnosticLog.BeginAttempt(
            implementationTaskId,
            requestTaskId,
            implementationTaskId + 10,
            checked((int)implementationTaskId + 20));
        await Task.Yield();
        RulePositioningDiagnosticLog.WriteInfo("concurrency.first", "summary");
        await Task.Yield();
        RulePositioningDiagnosticLog.WriteInfo("concurrency.second", "summary");
    }

    /// <summary>Asserts the correlation and sequence stream belonging to one attempt.</summary>
    private static void AssertAttempt(
        IEnumerable<JsonElement> diagnostics,
        long implementationTaskId,
        long requestTaskId)
    {
        List<JsonElement> attemptEvents = diagnostics
            .Where(Event => Event.GetProperty("implementation_task_id").GetInt64() == implementationTaskId)
            .OrderBy(Event => Event.GetProperty("sequence").GetInt64())
            .ToList();
        Assert.Multiple(() =>
        {
            Assert.That(attemptEvents, Has.Count.EqualTo(2));
            Assert.That(attemptEvents.Select(Event => Event.GetProperty("request_task_id").GetInt64()),
                Is.All.EqualTo(requestTaskId));
            Assert.That(attemptEvents.Select(Event => Event.GetProperty("sequence").GetInt64()),
                Is.EqualTo(new long[] { 1, 2 }));
            Assert.That(attemptEvents.Select(Event => Event.GetProperty("attempt_id").GetString()).Distinct().Count(),
                Is.EqualTo(1));
        });
    }

    /// <summary>Extracts compact diagnostic JSON objects from ordinary formatted log lines.</summary>
    private static List<JsonElement> ParseDiagnostics(string output)
    {
        const string prefix = "RPDIAG ";
        return
        [
            .. output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                .Where(Line => Line.Contains(prefix, StringComparison.Ordinal))
                .Select(Line =>
                {
                    int jsonStart = Line.IndexOf(prefix, StringComparison.Ordinal) + prefix.Length;
                    using JsonDocument document = JsonDocument.Parse(Line[jsonStart..]);
                    return document.RootElement.Clone();
                })
        ];
    }
}
