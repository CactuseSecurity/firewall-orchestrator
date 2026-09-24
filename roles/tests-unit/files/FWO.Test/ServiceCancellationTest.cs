using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Middleware.Server;
using FWO.Middleware.Server.Services;
using FWO.Services;
using FWO.Services.Modelling;
using FWO.Services.Workflow;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    /// <summary>
    /// Cooperative cancellation of the services behind the Quartz jobs: a requested cancellation
    /// stops at the next checkpoint, skips steps that assume a complete run and still performs cleanup.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class ServiceCancellationTest
    {
        [Test]
        public void ExternalRequestSender_PreCanceledToken_DoesNotLockRequests()
        {
            LockingExternalRequestApiConnection apiConnection = new(requestCount: 2);
            ExternalRequestSender sender = new(apiConnection, new SimulatedGlobalConfig());
            apiConnection.Queries.Clear();
            using CancellationTokenSource cancellationTokenSource = new();
            cancellationTokenSource.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(async () => await sender.Run(cancellationTokenSource.Token));
            Assert.That(apiConnection.Queries, Is.Empty);
        }

        [Test]
        public void ExternalRequestSender_CanceledAfterLocking_ReleasesAllLocks()
        {
            using CancellationTokenSource cancellationTokenSource = new();
            LockingExternalRequestApiConnection apiConnection = new(requestCount: 3)
            {
                OnLock = cancellationTokenSource.Cancel
            };
            ExternalRequestSender sender = new(apiConnection, new SimulatedGlobalConfig());
            apiConnection.Queries.Clear();

            Assert.ThrowsAsync<OperationCanceledException>(async () => await sender.Run(cancellationTokenSource.Token));

            Assert.That(apiConnection.Queries[0], Is.EqualTo(ExtRequestQueries.getAndLockOpenRequests));
            Assert.That(apiConnection.Queries.Skip(1), Is.All.EqualTo(ExtRequestQueries.updateExternalRequestLock));
            Assert.That(apiConnection.UnlockedIds, Is.EquivalentTo(new long[] { 1, 2, 3 }));
        }

        [Test]
        public void AppDataImport_PreCanceledToken_StopsBeforeQuerying()
        {
            RecordingApiConnection apiConnection = new();
            using AppDataImport import = new(apiConnection, new SimulatedGlobalConfig());
            apiConnection.Queries.Clear();

            Assert.ThrowsAsync<OperationCanceledException>(async () => await import.Run(CanceledToken()));
            Assert.That(apiConnection.Queries, Is.Empty);
        }

        [Test]
        public void AppDataImport_CanceledDuringImport_DoesNotDeactivateMissingApps()
        {
            RecordingApiConnection apiConnection = new()
            {
                Responses =
                {
                    [OwnerQueries.getOwnersWithNetworks] = new List<FwoOwner>
                    {
                        new() { Id = 1, ExtAppId = "APP-1", ImportSource = "SRC-A", Active = true }
                    }
                }
            };
            using AppDataImport import = new(apiConnection, new SimulatedGlobalConfig());
            SetPrivateField(import, "ImportedApps", new List<ModellingImportAppData>
            {
                new() { Name = "Imported2", ExtAppId = "APP-2", ImportSource = "SRC-A" }
            });
            MethodInfo importApps = typeof(AppDataImport).GetMethod("ImportApps", BindingFlags.NonPublic | BindingFlags.Instance)!;

            Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await (Task)importApps.Invoke(import, ["apps.json", new OwnerChangeImportTracker(apiConnection), CanceledToken()])!);
            Assert.That(apiConnection.Queries, Does.Not.Contain(OwnerQueries.deactivateOwner));
        }

        [Test]
        public void AdjustAppServerNames_PreCanceledToken_DoesNotRenameAppServers()
        {
            RecordingApiConnection apiConnection = new()
            {
                Responses =
                {
                    [ModellingQueries.getAllAppServers] = new List<ModellingAppServer> { new() { Id = 1, Name = "", Ip = "10.0.0.1" } }
                }
            };
            using UserConfig userConfig = UserConfig.ForGlobalSettings(new SimulatedGlobalConfig(), apiConnection);
            apiConnection.Queries.Clear();

            Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await AppServerHelper.AdjustAppServerNames(apiConnection, userConfig, CanceledToken()));
            Assert.That(apiConnection.Queries, Is.EqualTo(new[] { ModellingQueries.getAllAppServers }));
        }

        [Test]
        public void LogDataImport_PreCanceledToken_StopsBeforeQuerying()
        {
            RecordingApiConnection apiConnection = new();
            LogDataImport import = new(apiConnection, new SimulatedGlobalConfig());

            Assert.ThrowsAsync<OperationCanceledException>(async () => await import.Run(CanceledToken()));
            Assert.That(apiConnection.Queries, Is.Empty);
        }

        [Test]
        public void RecertCheck_PreCanceledToken_StopsBeforeQuerying()
        {
            RecordingApiConnection apiConnection = new();
            RecertCheck recertCheck = new(apiConnection, new SimulatedGlobalConfig(), new TokenLifetimeProvider());
            apiConnection.Queries.Clear();

            Assert.ThrowsAsync<OperationCanceledException>(async () => await recertCheck.CheckRecertifications(CanceledToken()));
            Assert.That(apiConnection.Queries, Is.Empty);
        }

        [Test]
        public void OwnerActiveRuleCheck_CanceledAfterLoadingOwners_SendsNoNotifications()
        {
            using CancellationTokenSource cancellationTokenSource = new();
            RecordingApiConnection apiConnection = new()
            {
                Responses =
                {
                    [OwnerQueries.getOwners] = new List<FwoOwner> { new() { Id = 1, Name = "Owner A", DecommDate = new DateTime(2026, 1, 1) } }
                },
                OnQuery = { [OwnerQueries.getOwners] = cancellationTokenSource.Cancel }
            };
            OwnerActiveRuleCheck check = new(apiConnection, new SimulatedGlobalConfig());

            Assert.ThrowsAsync<OperationCanceledException>(async () => await check.CheckActiveRulesByScheduler(cancellationTokenSource.Token));
            Assert.That(apiConnection.Queries, Is.EqualTo(new[] { OwnerQueries.getOwners }));
        }

        [Test]
        public void ModellingVarianceAnalysis_PreCanceledToken_WritesNoConnectionStatus()
        {
            RecordingApiConnection apiConnection = new();
            ModellingVarianceAnalysis varianceAnalysis = new(apiConnection, new ExtStateHandler(apiConnection), new SimulatedUserConfig(),
                new FwoOwner { Id = 1, Name = "App1" }, DefaultInit.DoNothing);
            apiConnection.Queries.Clear();

            Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await varianceAnalysis.AnalyseConnsForStatusAsync([new ModellingConnection { Id = 1 }], CanceledToken()));
            Assert.That(apiConnection.Queries, Does.Not.Contain(ModellingQueries.updateConnectionProperties));
        }

        private static CancellationToken CanceledToken()
        {
            return new CancellationToken(canceled: true);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(target.GetType().FullName, fieldName);
            field.SetValue(target, value);
        }

        /// <summary>
        /// Records every query and answers with configured responses, or an empty default otherwise.
        /// </summary>
        private sealed class RecordingApiConnection : JobTestApiConnectionBase
        {
            public Dictionary<string, object> Responses { get; } = [];
            public Dictionary<string, Action> OnQuery { get; } = [];

            protected override Task<QueryResponseType> HandleQueryAsync<QueryResponseType>(string query, object? variables, string? operationName, QueryChunkingOptions? chunkingOptions)
            {
                if (OnQuery.TryGetValue(query, out Action? onQuery))
                {
                    onQuery();
                }
                if (Responses.TryGetValue(query, out object? response))
                {
                    return Task.FromResult((QueryResponseType)response);
                }
                return ReturnEmptyOrDefault<QueryResponseType>();
            }
        }

        private sealed class LockingExternalRequestApiConnection(int requestCount) : JobTestApiConnectionBase
        {
            public Action? OnLock { get; init; }
            public List<long> UnlockedIds { get; } = [];

            protected override Task<QueryResponseType> HandleQueryAsync<QueryResponseType>(string query, object? variables, string? operationName, QueryChunkingOptions? chunkingOptions)
            {
                if (query == ExtRequestQueries.getAndLockOpenRequests)
                {
                    OnLock?.Invoke();
                    ExternalRequestDataHelper lockedRequests = new()
                    {
                        ExternalRequests = [.. Enumerable.Range(1, requestCount).Select(id => new ExternalRequest { Id = id, Locked = true })]
                    };
                    return Task.FromResult((QueryResponseType)(object)lockedRequests);
                }
                if (query == ExtRequestQueries.updateExternalRequestLock)
                {
                    long id = (long)variables!.GetType().GetProperty("id")!.GetValue(variables)!;
                    UnlockedIds.Add(id);
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { UpdatedIdLong = id });
                }
                return ReturnEmptyOrDefault<QueryResponseType>();
            }
        }
    }
}
