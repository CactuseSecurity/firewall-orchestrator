using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.Middleware.Server.Controllers;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Covers the author attribution of the audit-proof change trail (GHSA-6g84, usergroupresolver-001):
/// the endpoint names the user behind changer_id, never the free-text changer a writer supplied, and
/// only reports flagged changes of a known user other than the ticket's requester.
/// </summary>
[TestFixture]
internal class WorkflowChangeHistoryAuthorTest
{
    private const long kTicketId = 1234;
    private const int kPlannerId = 8;
    private const int kRequesterId = 3;
    private const string kPlannerName = "planner";
    private const string kForgedName = "requester-of-someone-else";
    private static readonly DateTime kChangeTime = new(2026, 11, 7, 10, 0, 0, DateTimeKind.Unspecified);

    [Test]
    public void HistoryQueriesSelectTheChangerUser()
    {
        Assert.Multiple(() =>
        {
            Assert.That(RequestQueries.getAuditProofCriticalChangesForTicket, Does.Contain("changer_user {"));
            Assert.That(RequestQueries.getAuditProofCriticalChangesForTicket, Does.Contain("uiuser_username"));
            Assert.That(RequestQueries.getWorkflowTaskHistoryForTicket, Does.Contain("changer_user {"));
            Assert.That(RequestQueries.getWorkflowTaskHistoryForTicket, Does.Contain("uiuser_username"));
        });
    }

    [Test]
    public async Task ChangesNameTheResolvedUserInsteadOfTheStoredChanger()
    {
        AuthorApiConnection apiConnection = new() { Entries = new() { ForgedEntry(ChangeHistoryObjectType.RequestTask) } };

        GetAuditProofCriticalChangesResponse response = await GetChanges(apiConnection);

        Assert.Multiple(() =>
        {
            Assert.That(response.Changes, Has.Count.EqualTo(1));
            Assert.That(response.Changes[0].ChangeUserName, Is.EqualTo(kPlannerName));
            Assert.That(response.Changes[0].ChangeUserId, Is.EqualTo(kPlannerId));
        });
    }

    [Test]
    public async Task ManualImplementationTaskChangesNameTheResolvedUser()
    {
        AuthorApiConnection apiConnection = new()
        {
            Entries = new() { ForgedEntry(ChangeHistoryObjectType.ImplementationTask) },
            TaskHistory = new() { ForgedEntry(ChangeHistoryObjectType.ImplementationTask) }
        };

        GetAuditProofCriticalChangesResponse response = await GetChanges(apiConnection);

        Assert.Multiple(() =>
        {
            Assert.That(response.TaskDiff, Is.Not.Null);
            Assert.That(response.TaskDiff!.ManualImplementationTaskChanges, Has.Count.EqualTo(1));
            Assert.That(response.TaskDiff.ManualImplementationTaskChanges[0].ChangeUserName, Is.EqualTo(kPlannerName));
        });
    }

    [Test]
    public async Task UserNameFilterMatchesTheResolvedUserOnly()
    {
        AuthorApiConnection apiConnection = new() { Entries = new() { ForgedEntry(ChangeHistoryObjectType.RequestTask) } };

        GetAuditProofCriticalChangesResponse byForgedName = await GetChanges(apiConnection, kForgedName);
        GetAuditProofCriticalChangesResponse byResolvedName = await GetChanges(apiConnection, kPlannerName);

        Assert.Multiple(() =>
        {
            Assert.That(byForgedName.Changes, Is.Empty);
            Assert.That(byResolvedName.Changes, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task AutomatedChangeIsNotReported()
    {
        ModellingHistoryEntry automated = new()
        {
            ChangeTime = kChangeTime,
            Changer = "middleware-server",
            ChangerId = null,
            ChangeText = "Updated workflow ticket",
            AuditProofCritical = true
        };
        AuthorApiConnection apiConnection = new() { Entries = new() { automated } };

        GetAuditProofCriticalChangesResponse response = await GetChanges(apiConnection);

        Assert.That(response.Changes, Is.Empty);
    }

    [Test]
    public async Task FlaggedChangesOfTheRequesterAreNotReported()
    {
        // the requester flags changes of the own ticket, passing them off as someone else's manual change
        AuthorApiConnection apiConnection = new()
        {
            Entries = new() { ForgedEntry(ChangeHistoryObjectType.RequestTask, kRequesterId), ForgedEntry(ChangeHistoryObjectType.RequestTask) },
            TaskHistory = new() { ForgedEntry(ChangeHistoryObjectType.ImplementationTask, kRequesterId) }
        };

        GetAuditProofCriticalChangesResponse response = await GetChanges(apiConnection);

        Assert.Multiple(() =>
        {
            Assert.That(response.Changes, Has.Count.EqualTo(1), "the planner's change is still reported");
            Assert.That(response.Changes[0].ChangeUserId, Is.EqualTo(kPlannerId));
            Assert.That(response.TaskDiff!.ManualImplementationTaskChanges, Is.Empty);
        });
    }

    [Test]
    public async Task FlaggedChangesOfATicketWithoutRequesterAreNotReported()
    {
        AuthorApiConnection apiConnection = new() { Entries = new() { ForgedEntry(ChangeHistoryObjectType.RequestTask) }, RequesterId = null };

        GetAuditProofCriticalChangesResponse response = await GetChanges(apiConnection);

        Assert.That(response.Changes, Is.Empty);
    }

    [Test]
    public async Task AnUnresolvableUserIsNamedByIdNotByTheStoredChanger()
    {
        ModellingHistoryEntry entry = ForgedEntry(ChangeHistoryObjectType.RequestTask);
        entry.ChangerUser = null;
        AuthorApiConnection apiConnection = new() { Entries = new() { entry } };

        GetAuditProofCriticalChangesResponse response = await GetChanges(apiConnection);

        Assert.That(response.Changes[0].ChangeUserName, Is.EqualTo(kPlannerId.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }

    private static ModellingHistoryEntry ForgedEntry(ChangeHistoryObjectType objectType, int changerId = kPlannerId)
    {
        return new ModellingHistoryEntry
        {
            ChangeType = (int)ModellingTypes.ChangeType.Update,
            ObjectType = (int)objectType,
            ObjectId = 20,
            ChangeTime = kChangeTime,
            Changer = kForgedName,
            ChangerId = changerId,
            ChangerUser = new UiUser { DbId = changerId, Name = changerId == kPlannerId ? kPlannerName : "requester" },
            ChangeText = "Updated workflow task",
            AuditProofCritical = true
        };
    }

    private static async Task<GetAuditProofCriticalChangesResponse> GetChanges(AuthorApiConnection apiConnection, string? userName = null)
    {
        WorkflowChangeHistoryController controller = new(new WorkflowChangeHistoryService(apiConnection), new SimulatedGlobalConfig());
        GetAuditProofCriticalChangesRequest request = new() { TicketId = kTicketId };
        if (userName != null)
        {
            request.Options = new GetAuditProofCriticalChangesOptions { Filter = new AuditProofCriticalChangeFilter { ChangeUserName = userName } };
        }

        ActionResult<GetAuditProofCriticalChangesResponse> result = await controller.GetAuditProofCriticalChanges(request);
        return (GetAuditProofCriticalChangesResponse)((OkObjectResult)result.Result!).Value!;
    }

    private sealed class AuthorApiConnection : SimulatedApiConnection
    {
        public List<ModellingHistoryEntry> Entries { get; set; } = new();

        public List<ModellingHistoryEntry> TaskHistory { get; set; } = new();

        public int? RequesterId { get; set; } = kRequesterId;

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null,
            string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            object response = query switch
            {
                _ when query == RequestQueries.getAuditProofCriticalChangesForTicket => Entries,
                _ when query == RequestQueries.getWorkflowTaskHistoryForTicket => TaskHistory,
                _ when query == RequestQueries.getTicketById => new WfTicket
                {
                    Id = kTicketId,
                    Requester = RequesterId == null ? null : new UiUser { DbId = RequesterId.Value }
                },
                _ when query == RequestQueries.getTicketIdIfExists => new List<WfTicketBase> { new() { Id = kTicketId } },
                _ => throw new AssertionException($"Unexpected query: {query}")
            };
            return Task.FromResult((QueryResponseType)response);
        }
    }
}
