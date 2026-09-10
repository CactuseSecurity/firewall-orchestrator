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
        public async Task Rollback_WhenRollbackFails_ReportsErrorAndNoSuccess()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            await using BunitContext context = CreateContext(new ImportRollbackTestApiConn { FailOnRollback = true }, messages);

            IRenderedComponent<ImportRollback> cut = RenderComponent(context);
            await InvokePrivateTask(cut, "Rollback");

            // a failing rollback must be surfaced and must not show a success message
            Assert.That(messages.Exists(m => m.IsError), Is.True);
            Assert.That(messages.Exists(m => !m.IsError && m.Message == "Rollback done"), Is.False);
        }

        [Test]
        public async Task Rollback_WhenRollbackSucceeds_ReportsSuccess()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            ImportRollbackTestApiConn apiConn = new();
            await using BunitContext context = CreateContext(apiConn, messages);

            IRenderedComponent<ImportRollback> cut = RenderComponent(context);
            await InvokePrivateTask(cut, "Rollback");

            // data rollback and import_control deletion travel in one mutation document
            Assert.That(apiConn.RollbackCalls, Is.EqualTo(1));
            Assert.That(messages.Exists(m => !m.IsError && m.Message == "Rollback done"), Is.True);
            Assert.That(messages.Exists(m => m.IsError), Is.False);
        }

        [Test]
        public async Task FullMgmRollback_WhenRollbackFails_DoesNotDeleteLatestConfigOrReportSuccess()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            ImportRollbackTestApiConn apiConn = new() { FailOnRollback = true };
            await using BunitContext context = CreateContext(apiConn, messages);

            IRenderedComponent<ImportRollback> cut = RenderComponent(context);
            await InvokePrivateTask(cut, "FullMgmRollback");

            // a failing rollback must not delete the latest config and must not claim success
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

            Assert.That(apiConn.RollbackCalls, Is.EqualTo(1));
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
            // full rollback is gated behind the "allowFullRollback" setting; enable it so the
            // rollback paths under test actually execute instead of being blocked by the guard
            context.Services.AddSingleton<UserConfig>(UserConfig.ForTextOnly(new SimulatedGlobalConfig { AllowFullRollback = true }));
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
        public bool FailOnRollback { get; init; }

        public int RollbackCalls { get; private set; }
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

            if (query == ImportQueries.rollbackImport)
            {
                RollbackCalls++;
                if (FailOnRollback)
                {
                    throw new InvalidOperationException("rollbackImport failed");
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
