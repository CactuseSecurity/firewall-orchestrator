using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public class UiImportRollbackTest
    {
        private const int kManagementId = 7;

        [Test]
        public async Task Rollback_WhenDeleteImportControlFails_ReportsErrorAndNoSuccess()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            await using BunitContext context = CreateContext(new ImportRollbackTestApiConn { FailOnDeleteImportControl = true }, messages);

            IRenderedComponent<ImportRollback> cut = RenderComponent(context);
            await InvokePrivateTask(cut, "Rollback");

            // the data-only rollback succeeded but the import_control deletion failed:
            // the failure must be surfaced and no success message must be shown
            Assert.That(messages.Exists(m => m.IsError), Is.True);
            Assert.That(messages.Exists(m => !m.IsError && m.Message == "Rollback done"), Is.False);
        }

        [Test]
        public async Task Rollback_WhenRollbackDataFails_ReportsErrorAndNoSuccess()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            await using BunitContext context = CreateContext(new ImportRollbackTestApiConn { FailOnRollbackData = true }, messages);

            IRenderedComponent<ImportRollback> cut = RenderComponent(context);
            await InvokePrivateTask(cut, "Rollback");

            Assert.That(messages.Exists(m => m.IsError), Is.True);
            Assert.That(messages.Exists(m => !m.IsError && m.Message == "Rollback done"), Is.False);
        }

        [Test]
        public async Task Rollback_WhenBothMutationsSucceed_ReportsSuccess()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            ImportRollbackTestApiConn apiConn = new();
            await using BunitContext context = CreateContext(apiConn, messages);

            IRenderedComponent<ImportRollback> cut = RenderComponent(context);
            await InvokePrivateTask(cut, "Rollback");

            Assert.That(apiConn.RollbackDataCalls, Is.EqualTo(1));
            Assert.That(apiConn.DeleteImportControlCalls, Is.EqualTo(1));
            Assert.That(messages.Exists(m => !m.IsError && m.Message == "Rollback done"), Is.True);
            Assert.That(messages.Exists(m => m.IsError), Is.False);
        }

        [Test]
        public async Task FullMgmRollback_WhenDeleteImportControlFails_DoesNotDeleteLatestConfigOrReportSuccess()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            ImportRollbackTestApiConn apiConn = new() { FailOnDeleteImportControl = true };
            await using BunitContext context = CreateContext(apiConn, messages);

            IRenderedComponent<ImportRollback> cut = RenderComponent(context);
            await InvokePrivateTask(cut, "FullMgmRollback");

            // a failing rollback must not delete the latest config and must not claim success
            Assert.That(apiConn.DeleteLatestConfigCalls, Is.EqualTo(0));
            Assert.That(messages.Exists(m => m.IsError), Is.True);
            Assert.That(messages.Exists(m => !m.IsError && m.Message == "Rollback done"), Is.False);
        }

        [Test]
        public async Task FullMgmRollback_WhenRollbackDataFails_DoesNotDeleteLatestConfigOrReportSuccess()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            ImportRollbackTestApiConn apiConn = new() { FailOnRollbackData = true };
            await using BunitContext context = CreateContext(apiConn, messages);

            IRenderedComponent<ImportRollback> cut = RenderComponent(context);
            await InvokePrivateTask(cut, "FullMgmRollback");

            Assert.That(apiConn.DeleteLatestConfigCalls, Is.EqualTo(0));
            Assert.That(messages.Exists(m => m.IsError), Is.True);
            Assert.That(messages.Exists(m => !m.IsError && m.Message == "Rollback done"), Is.False);
        }

        [Test]
        public async Task FullMgmRollback_WhenRollbackSucceeds_DeletesLatestConfigAndReportsSuccess()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            ImportRollbackTestApiConn apiConn = new();
            await using BunitContext context = CreateContext(apiConn, messages);

            IRenderedComponent<ImportRollback> cut = RenderComponent(context);
            await InvokePrivateTask(cut, "FullMgmRollback");

            Assert.That(apiConn.RollbackDataCalls, Is.EqualTo(1));
            Assert.That(apiConn.DeleteImportControlCalls, Is.EqualTo(1));
            Assert.That(apiConn.DeleteLatestConfigCalls, Is.EqualTo(1));
            Assert.That(messages.Exists(m => !m.IsError && m.Message == "Rollback done"), Is.True);
            Assert.That(messages.Exists(m => m.IsError), Is.False);
        }

        private Action<Exception?, string, string, bool> displayMessageInUi = (_, _, _, _) => { };

        private IRenderedComponent<ImportRollback> RenderComponent(BunitContext context)
        {
            IRenderedComponent<CascadingValue<Action<Exception?, string, string, bool>>> host =
                context.Render<CascadingValue<Action<Exception?, string, string, bool>>>(parameters => parameters
                    .Add(p => p.Value, displayMessageInUi)
                    .AddChildContent<ImportRollback>(component => component
                        .Add(p => p.ManagementId, kManagementId)));
            return host.FindComponent<ImportRollback>();
        }

        private BunitContext CreateContext(
            ImportRollbackTestApiConn apiConn,
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddSingleton<ApiConnection>(apiConn);
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            displayMessageInUi = (exception, title, message, isError) => messages.Add((exception, title, message, isError));
            return context;
        }

        private static async Task InvokePrivateTask(IRenderedComponent<ImportRollback> cut, string methodName)
        {
            await cut.InvokeAsync(async () =>
            {
                MethodInfo method = typeof(ImportRollback).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new MissingMethodException(typeof(ImportRollback).FullName, methodName);
                if (method.Invoke(cut.Instance, null) is Task task)
                {
                    await task;
                }
            });
        }
    }

    internal sealed class ImportRollbackTestApiConn : SimulatedApiConnection
    {
        public bool FailOnRollbackData { get; init; }
        public bool FailOnDeleteImportControl { get; init; }

        public int RollbackDataCalls { get; private set; }
        public int DeleteImportControlCalls { get; private set; }
        public int DeleteLatestConfigCalls { get; private set; }

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            if (query == ImportQueries.getLastImport)
            {
                return Task.FromResult((QueryResponseType)(object)new List<ImportControl> { new() { ControlId = 42, MgmId = 7 } });
            }

            if (query == ImportQueries.getImportIdsByManagement)
            {
                return Task.FromResult((QueryResponseType)(object)new List<ImportControl> { new() { ControlId = 42, MgmId = 7 } });
            }

            if (query == ImportQueries.rollbackImportData)
            {
                RollbackDataCalls++;
                if (FailOnRollbackData)
                {
                    throw new InvalidOperationException("rollbackImportData failed");
                }
                return Task.FromResult((QueryResponseType)(object)new ReturnId());
            }

            if (query == ImportQueries.deleteImportControl)
            {
                DeleteImportControlCalls++;
                if (FailOnDeleteImportControl)
                {
                    throw new InvalidOperationException("deleteImportControl failed");
                }
                return Task.FromResult((QueryResponseType)(object)new ReturnId());
            }

            if (query == ImportQueries.deleteLatestConfigOfManagement)
            {
                DeleteLatestConfigCalls++;
                return Task.FromResult((QueryResponseType)(object)new ReturnId());
            }

            throw new NotImplementedException($"Unhandled query for {typeof(QueryResponseType).Name}");
        }
    }
}
