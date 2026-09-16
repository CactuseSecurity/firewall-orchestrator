using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Middleware.Server.Controllers;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal class NotificationControllerTest
{
    [Test]
    public async Task InsertLog_ReturnsInsertedId()
    {
        NotificationController controller = new(new NotificationControllerApiConnection { InsertedId = 17 });
        NotificationLogInsertEntry entry = new() { NotificationId = 4, Subject = "subject" };

        ActionResult<int> result = await controller.InsertLog(entry);

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        Assert.That(((OkObjectResult)result.Result!).Value, Is.EqualTo(17));
    }

    [Test]
    public async Task InsertLog_ReturnsInternalServerErrorWhenPersistenceFails()
    {
        NotificationController controller = new(new NotificationControllerApiConnection { ThrowOnInsert = true });

        ActionResult<int> result = await controller.InsertLog(new NotificationLogInsertEntry());

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        Assert.That(((ObjectResult)result.Result!).StatusCode, Is.EqualTo(500));
    }

    [Test]
    public async Task UpdateLog_ReturnsSuccess()
    {
        NotificationController controller = new(new NotificationControllerApiConnection());

        ActionResult<bool> result = await controller.UpdateLog(new NotificationLogUpdateParameters
        {
            Id = 17,
            Status = NotificationLogStatus.Sent,
            Error = ""
        });

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        Assert.That(((OkObjectResult)result.Result!).Value, Is.True);
    }

    [Test]
    public async Task UpdateLog_ReturnsInternalServerErrorWhenPersistenceFails()
    {
        NotificationController controller = new(new NotificationControllerApiConnection { ThrowOnUpdate = true });

        ActionResult<bool> result = await controller.UpdateLog(new NotificationLogUpdateParameters());

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        Assert.That(((ObjectResult)result.Result!).StatusCode, Is.EqualTo(500));
    }

    private sealed class NotificationControllerApiConnection : SimulatedApiConnection
    {
        public int InsertedId { get; init; }
        public bool ThrowOnInsert { get; init; }
        public bool ThrowOnUpdate { get; init; }

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null,
            string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            if (query == NotificationQueries.insertNotificationLog && typeof(QueryResponseType) == typeof(ReturnIdWrapper))
            {
                if (ThrowOnInsert)
                {
                    throw new InvalidOperationException("insert failed");
                }

                return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper
                {
                    ReturnIds = [new ReturnId { Id = InsertedId }]
                });
            }

            if (query == NotificationQueries.updateNotificationLog && typeof(QueryResponseType) == typeof(ReturnId))
            {
                if (ThrowOnUpdate)
                {
                    throw new InvalidOperationException("update failed");
                }

                return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = 1 });
            }

            throw new InvalidOperationException($"Unexpected query: {query}");
        }
    }
}
