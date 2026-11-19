using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.File;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Middleware.Server;
using FWO.Test.Helpers;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Live Hasura coverage for the middleware effective-state snapshot authorization boundary.
/// </summary>
[TestFixture]
[NonParallelizable]
[RequiresIntegrationEnvironment]
internal class EffectiveRequestStateMutationIntegrationTest
{
    private static readonly string[] kForbiddenBroadUpdateFields =
    [
        "request_action", "task_id", "field", "user_id", "device_id"
    ];

    private const string kReadSnapshot = """
        query readEffectiveRequestElementSnapshot($id: bigint!) {
          request_reqelement_by_pk(id: $id) {
            id
            task_id
            ip
            ip_end
            port
            port_end
            ip_proto_id
            group_name
            name
            start_time
            end_time
          }
        }
        """;

    private const string kDeleteTicket = """
        mutation deleteEffectiveRequestStateSmokeTicket($id: bigint!) {
          delete_request_ticket_by_pk(id: $id) {
            deletedIdLong: id
          }
        }
        """;

    /// <summary>
    /// Proves the service role can write the narrow snapshot but Hasura rejects the broad legacy update.
    /// </summary>
    [Test]
    [Category("Integration")]
    public async Task MiddlewareServer_AllowsSnapshotMutationAndRejectsBroadRequestElementUpdate()
    {
        JwtWriter writer = new(ConfigFile.JwtPrivateKey);
        using GraphQlApiConnection admin = new(ConfigFile.ApiServerUri, CreateAdminToken(writer));
        using GraphQlApiConnection middleware = new(ConfigFile.ApiServerUri,
            writer.CreateJWTMiddlewareServer(TimeSpan.FromMinutes(10)));
        SmokeRowIds rowIds = new();
        Exception? operationFailure = null;

        try
        {
            await SeedRequestElement(admin, rowIds);
            DateTime startTime = new(2026, 11, 19, 8, 30, 0, DateTimeKind.Utc);
            DateTime endTime = startTime.AddHours(1);

            ReturnId updated = await middleware.SendQueryAsync<ReturnId>(
                RequestQueries.updateEffectiveRequestElementSnapshot,
                SnapshotVariables(rowIds.ElementId, startTime, endTime));
            WfReqElement readback = await middleware.SendQueryAsync<WfReqElement>(
                kReadSnapshot, new { id = rowIds.ElementId });

            Assert.Multiple(() => AssertSnapshot(updated, readback, rowIds.ElementId,
                rowIds.TaskId, startTime, endTime));

            Dictionary<string, object?> broadVariables = BroadUpdateVariables(rowIds.ElementId, rowIds.TaskId);
            ReturnId adminBroadUpdate = await admin.SendQueryAsync<ReturnId>(
                RequestQueries.updateRequestElement, broadVariables);
            Assert.That(adminBroadUpdate.UpdatedIdLong, Is.EqualTo(rowIds.ElementId),
                "The broad mutation variables were not database-valid for the admin control call.");

            InvalidOperationException denial = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await middleware.SendQueryAsync<ReturnId>(RequestQueries.updateRequestElement,
                    broadVariables))!;
            Assert.That(IsForbiddenBroadMutationError(denial), Is.True,
                "Middleware rejection did not identify a forbidden broad update field.");
        }
        catch (Exception exception)
        {
            operationFailure = exception;
            throw;
        }
        finally
        {
            await Cleanup(admin, rowIds, operationFailure);
        }
    }

    private static async Task SeedRequestElement(GraphQlApiConnection admin, SmokeRowIds rowIds)
    {
        string uniqueName = $"effective-state-smoke-{Guid.NewGuid():N}";
        rowIds.TicketId = (await admin.SendQueryAsync<ReturnIdWrapper>(RequestQueries.newTicket,
            TicketSeedVariables(uniqueName))).ReturnIds!.Single().NewIdLong;
        rowIds.TaskId = (await admin.SendQueryAsync<ReturnIdWrapper>(RequestQueries.newRequestTask,
            RequestTaskSeedVariables(uniqueName, rowIds.TicketId))).ReturnIds!.Single().NewIdLong;
        rowIds.ElementId = (await admin.SendQueryAsync<ReturnIdWrapper>(RequestQueries.newRequestElement,
            RequestElementSeedVariables(rowIds.TaskId))).ReturnIds!.Single().NewIdLong;
    }

    private static Dictionary<string, object?> TicketSeedVariables(string title)
    {
        return new Dictionary<string, object?>
        {
            ["title"] = title,
            ["state"] = 0,
            ["locked"] = false
        };
    }

    private static Dictionary<string, object?> RequestTaskSeedVariables(string title, long ticketId)
    {
        return new Dictionary<string, object?>
        {
            ["title"] = title,
            ["ticketId"] = ticketId,
            ["state"] = 0,
            ["taskType"] = WfTaskType.group_create.ToString(),
            ["requestAction"] = RequestAction.create.ToString(),
            ["locked"] = false
        };
    }

    private static Dictionary<string, object?> RequestElementSeedVariables(long taskId)
    {
        return new Dictionary<string, object?>
        {
            ["taskId"] = taskId,
            ["field"] = ElemFieldType.source.ToString(),
            ["requestAction"] = RequestAction.create.ToString()
        };
    }

    private static Dictionary<string, object?> BroadUpdateVariables(long elementId, long taskId)
    {
        return new Dictionary<string, object?>
        {
            ["id"] = elementId,
            ["requestAction"] = RequestAction.create.ToString(),
            ["taskId"] = taskId,
            ["field"] = ElemFieldType.source.ToString(),
            ["userId"] = null
        };
    }

    private static bool IsForbiddenBroadMutationError(InvalidOperationException exception)
    {
        return exception.Message.Contains("not found in type", StringComparison.OrdinalIgnoreCase)
            && kForbiddenBroadUpdateFields.Any(field =>
                exception.Message.Contains(field, StringComparison.OrdinalIgnoreCase));
    }

    private static Dictionary<string, object?> SnapshotVariables(long elementId, DateTime startTime,
        DateTime endTime)
    {
        return new Dictionary<string, object?>
        {
            ["id"] = elementId,
            ["ip"] = "198.51.100.25/32",
            ["ipEnd"] = "198.51.100.26/32",
            ["port"] = 443,
            ["portEnd"] = 444,
            ["proto"] = 6,
            ["groupName"] = "integration-snapshot-group",
            ["name"] = "integration-snapshot-name",
            ["startTime"] = startTime,
            ["endTime"] = endTime
        };
    }

    private static void AssertSnapshot(ReturnId updated, WfReqElement readback, long elementId,
        long taskId, DateTime startTime, DateTime endTime)
    {
        Assert.That(updated.UpdatedIdLong, Is.EqualTo(elementId));
        Assert.That(readback.Id, Is.EqualTo(elementId));
        Assert.That(readback.TaskId, Is.EqualTo(taskId));
        Assert.That(readback.IpString, Is.EqualTo("198.51.100.25/32"));
        Assert.That(readback.IpEnd, Is.EqualTo("198.51.100.26/32"));
        Assert.That(readback.Port, Is.EqualTo(443));
        Assert.That(readback.PortEnd, Is.EqualTo(444));
        Assert.That(readback.ProtoId, Is.EqualTo(6));
        Assert.That(readback.GroupName, Is.EqualTo("integration-snapshot-group"));
        Assert.That(readback.Name, Is.EqualTo("integration-snapshot-name"));
        Assert.That(readback.StartTime, Is.EqualTo(startTime));
        Assert.That(readback.EndTime, Is.EqualTo(endTime));
    }

    private static async Task Cleanup(GraphQlApiConnection admin, SmokeRowIds rowIds,
        Exception? operationFailure)
    {
        List<Exception> cleanupFailures = [];
        if (rowIds.ElementId > 0)
        {
            try
            {
                await admin.SendQueryAsync<ReturnId>(RequestQueries.deleteRequestElement,
                    new { id = rowIds.ElementId });
            }
            catch (Exception exception)
            {
                cleanupFailures.Add(exception);
            }
        }
        if (rowIds.TaskId > 0)
        {
            try
            {
                await admin.SendQueryAsync<ReturnId>(RequestQueries.deleteRequestTask,
                    new { id = rowIds.TaskId });
            }
            catch (Exception exception)
            {
                cleanupFailures.Add(exception);
            }
        }
        if (rowIds.TicketId > 0)
        {
            try
            {
                await admin.SendQueryAsync<ReturnId>(kDeleteTicket, new { id = rowIds.TicketId });
            }
            catch (Exception exception)
            {
                cleanupFailures.Add(exception);
            }
        }
        if (cleanupFailures.Count > 0 && operationFailure is null)
        {
            throw new AggregateException("Effective-state mutation smoke cleanup failed.", cleanupFailures);
        }
        foreach (Exception cleanupFailure in cleanupFailures)
        {
            TestContext.Progress.WriteLine($"Effective-state mutation smoke cleanup also failed: {cleanupFailure.Message}");
        }
    }

    private static string CreateAdminToken(JwtWriter writer)
    {
        return writer.CreateJWT(new UiUser
        {
            Name = "effective-state-mutation-integration-smoke",
            DbId = 0,
            Roles = [Roles.Admin]
        }, TimeSpan.FromMinutes(10));
    }

    private sealed class SmokeRowIds
    {
        internal long TicketId { get; set; }
        internal long TaskId { get; set; }
        internal long ElementId { get; set; }
    }
}
