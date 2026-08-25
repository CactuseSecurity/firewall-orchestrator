using FWO.Basics.Comparer;
using FWO.Basics.Exceptions;
using FWO.Basics.Interfaces;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Data.Logging;
using FWO.Data.Middleware;
using FWO.Data.Workflow;
using FWO.ExternalSystems.CheckPoint;
using FWO.Services;
using FWO.Services.EventMediator.Events;
using FWO.Services.EventMediator.Interfaces;
using FWO.Services.Logging;
using FWO.Ui.Services;
using NUnit.Framework;
using System.Text.Json;

namespace FWO.Test
{
    [TestFixture]
    internal class CoverageInfrastructureTest
    {
        private static readonly (string Key, object? Value)[] kRequestFields =
        {
            ("Included", "value"),
            ("Ignored", null)
        };

        [Test]
        public void EventModels_ExposeTypedAndInterfaceEventArguments()
        {
            AppServerImportEvent appServerEvent = new(new AppServerImportEventArgs(true));
            FileUploadEvent fileUploadEvent = new(new FileUploadEventArgs(true, "import.csv"));
            CollectionChangedEvent collectionChangedEvent = new();

            Assert.That(appServerEvent.EventArgs!.Success, Is.True);
            Assert.That(fileUploadEvent.EventArgs!.FileName, Is.EqualTo("import.csv"));
            Assert.That(((IEvent)appServerEvent).EventArgs, Is.TypeOf<AppServerImportEventArgs>());
            Assert.That(((IEvent)fileUploadEvent).EventArgs, Is.TypeOf<FileUploadEventArgs>());
            ((IEvent)appServerEvent).EventArgs = new FileUploadEventArgs();
            ((IEvent)fileUploadEvent).EventArgs = new AppServerImportEventArgs();
            ((IEvent)collectionChangedEvent).EventArgs = new CollectionChangedEventArgs();
            AppServerImportEvent defaultAppServerEvent = new();
            FileUploadEvent defaultFileUploadEvent = new();

            defaultAppServerEvent.EventArgs!.Errors.Add(new CSVFileUploadErrorModel());
            defaultAppServerEvent.EventArgs.Appserver.Add("server");
            defaultFileUploadEvent.EventArgs!.Data = "uploaded";

            Assert.That(appServerEvent.EventArgs, Is.Null);
            Assert.That(fileUploadEvent.EventArgs, Is.Null);
            Assert.That(((IEvent)collectionChangedEvent).EventArgs, Is.TypeOf<CollectionChangedEventArgs>());
            Assert.That(defaultAppServerEvent.EventArgs.Errors, Has.Count.EqualTo(1));
            Assert.That(defaultAppServerEvent.EventArgs.Appserver, Is.EqualTo(new List<string> { "server" }));
            Assert.That(defaultFileUploadEvent.EventArgs.Data, Is.EqualTo("uploaded"));
            Assert.That(defaultFileUploadEvent.EventArgs.Error, Is.Not.Null);
            Assert.That(((CollectionChangedEventArgs)((IEvent)collectionChangedEvent).EventArgs!).Error, Is.Not.Null);
        }

        [Test]
        public void DataModels_ExposeDefaultsAndConstants()
        {
            Color color = new() { Name = "green" };
            ChangeLogRequest request = new()
            {
                Family = ChangeLogFamily.Manual,
                Object = ChangeLogObject.Gateway,
                Operation = ChangeLogOperation.Create,
                UserId = "test-user",
                Origin = ChangeLogOrigin.UiSettings
            };

            Assert.Multiple(() =>
            {
                Assert.That(color.Name, Is.EqualTo("green"));
                Assert.That(request.Fields, Is.Empty);
                Assert.That(FlowState.All, Is.EqualTo(new List<string>
                {
                    FlowState.Requested,
                    FlowState.Denied,
                    FlowState.Implemented,
                    FlowState.Removed
                }));
                Assert.That(CheckPointTaskTypes.GroupCreate, Is.EqualTo(nameof(WfTaskType.group_create)));
                Assert.That(CheckPointTaskTypes.InstallPolicy, Is.EqualTo("install_policy"));
            });
        }

        [Test]
        public void ChangeLogRequests_MapGatewayFields()
        {
            DateTime timestamp = new(2026, 7, 24, 8, 0, 0, DateTimeKind.Utc);
            GatewayChangeLogRequest changeRequest = new()
            {
                Family = ChangeLogFamily.Import,
                Operation = ChangeLogOperation.Update,
                UserId = "importer",
                Origin = ChangeLogOrigin.Import,
                Timestamp = timestamp,
                DeviceId = 11,
                DeviceName = "gateway",
                ManagementId = 22
            };
            GatewayPromptLogRequest promptRequest = new()
            {
                PromptEvent = PromptLogEvent.Created,
                Operation = ChangeLogOperation.Create,
                UserId = "autodiscovery",
                Origin = ChangeLogOrigin.Autodiscovery,
                Timestamp = timestamp,
                DeviceId = 11,
                DeviceName = "gateway",
                ManagementId = 22,
                ManagementName = "manager"
            };

            ChangeLogRequest mappedChangeRequest = changeRequest.ToChangeLogRequest();
            PromptLogRequest mappedPromptRequest = promptRequest.ToPromptLogRequest();

            Assert.Multiple(() =>
            {
                Assert.That(mappedChangeRequest.Object, Is.EqualTo(ChangeLogObject.Gateway));
                Assert.That(mappedChangeRequest.Fields, Has.Length.EqualTo(3));
                Assert.That(mappedChangeRequest.Timestamp, Is.EqualTo(timestamp));
                Assert.That(mappedPromptRequest.Object, Is.EqualTo(ChangeLogObject.Gateway));
                Assert.That(mappedPromptRequest.Fields, Has.Length.EqualTo(4));
                Assert.That(mappedPromptRequest.Timestamp, Is.EqualTo(timestamp));
            });
        }

        [TestCase(nameof(ActionCode.AddManagement), "{\"name\":\"manager\",\"hostname\":\"manager.example.test\"}", ChangeLogObject.Management, ChangeLogOperation.Create)]
        [TestCase(nameof(ActionCode.DeleteManagement), null, ChangeLogObject.Management, ChangeLogOperation.Delete)]
        [TestCase(nameof(ActionCode.ReactivateManagement), null, ChangeLogObject.Management, ChangeLogOperation.Activate)]
        [TestCase(nameof(ActionCode.DeleteGateway), null, ChangeLogObject.Gateway, ChangeLogOperation.Delete)]
        [TestCase(nameof(ActionCode.AddGatewayToNewManagement), "{\"name\":\"gateway\",\"management\":{\"id\":7,\"name\":\"manager\"}}", ChangeLogObject.Gateway, ChangeLogOperation.Create)]
        [TestCase(nameof(ActionCode.AddGatewayToExistingManagement), "{\"name\":\"gateway\",\"management\":{\"id\":7,\"name\":\"manager\"}}", ChangeLogObject.Gateway, ChangeLogOperation.Create)]
        [TestCase(nameof(ActionCode.ReactivateGateway), null, ChangeLogObject.Gateway, ChangeLogOperation.Activate)]
        public void AutodiscoveryLogMapper_MapsSupportedActions(string actionType, string? jsonData, ChangeLogObject expectedObject, ChangeLogOperation expectedOperation)
        {
            ActionItem action = new()
            {
                ActionType = actionType,
                JsonData = jsonData,
                ManagementId = 7,
                DeviceId = 8
            };

            bool mapped = AutodiscoveryLogMapper.TryMapPromptAction(action, out AutodiscoveryLogMapper.PromptLogData? logData);

            Assert.Multiple(() =>
            {
                Assert.That(mapped, Is.True);
                Assert.That(logData, Is.Not.Null);
                Assert.That(logData!.Object, Is.EqualTo(expectedObject));
                Assert.That(logData.Operation, Is.EqualTo(expectedOperation));
            });
        }

        [Test]
        public void AutodiscoveryLogMapper_HandlesUnsupportedAndMalformedData()
        {
            ActionItem unsupported = new() { ActionType = "unsupported" };
            ActionItem malformed = new() { ActionType = nameof(ActionCode.AddManagement), JsonData = "not-json" };

            Assert.Multiple(() =>
            {
                Assert.That(AutodiscoveryLogMapper.TryMapPromptAction(unsupported, out _), Is.False);
                Assert.That(AutodiscoveryLogMapper.TryMapPromptAction(malformed, out AutodiscoveryLogMapper.PromptLogData? logData), Is.True);
                Assert.That(logData!.Fields[1].Value, Is.Null);
            });
        }

        [Test]
        public async Task ChangeLogHelper_FormatsAndLogsAllSupportedValues()
        {
            ChangeLogRequest request = new()
            {
                Family = ChangeLogFamily.Manual,
                Object = ChangeLogObject.Matrix,
                Operation = ChangeLogOperation.Create,
                UserId = "tester",
                Origin = ChangeLogOrigin.UiSettings,
                Fields = kRequestFields
            };

            await ChangeLogHelper.LogChange(request);
            await ChangeLogHelper.LogChange(request with { Family = ChangeLogFamily.Import });
            await ChangeLogHelper.LogChange(request with { Family = (ChangeLogFamily)99 });

            Assert.Multiple(() =>
            {
                Assert.That(ChangeLogHelper.FormatFields(("First", 1), ("Second", "two"), ("Empty", null)), Is.EqualTo("First: 1; Second: two"));
                Assert.That(ChangeLogHelper.FormatFields(("Empty", null)), Is.Empty);
                Assert.That(ChangeLogHelper.GetOriginName(ChangeLogOrigin.UiSettings), Is.EqualTo("UI"));
                Assert.That(ChangeLogHelper.GetOriginName(ChangeLogOrigin.Autodiscovery), Is.EqualTo("Autodiscovery"));
                Assert.That(ChangeLogHelper.GetOriginName(ChangeLogOrigin.Import), Is.EqualTo("Import"));
                Assert.That(ChangeLogHelper.GetOriginName((ChangeLogOrigin)99), Is.EqualTo("99"));
                Assert.That(ChangeLogHelper.GetObjectName(ChangeLogObject.Management), Is.EqualTo("Management"));
                Assert.That(ChangeLogHelper.GetObjectName(ChangeLogObject.Gateway), Is.EqualTo("Gateway"));
                Assert.That(ChangeLogHelper.GetObjectName((ChangeLogObject)99), Is.EqualTo("99"));
                Assert.That(ChangeLogHelper.GetOperationName(ChangeLogOperation.Update), Is.EqualTo("Update"));
                Assert.That(ChangeLogHelper.GetOperationName(ChangeLogOperation.Delete), Is.EqualTo("Delete"));
                Assert.That(ChangeLogHelper.GetOperationName(ChangeLogOperation.SetRemoved), Is.EqualTo("Set to removed"));
                Assert.That(ChangeLogHelper.GetOperationName(ChangeLogOperation.Disable), Is.EqualTo("Disable"));
                Assert.That(ChangeLogHelper.GetOperationName(ChangeLogOperation.Activate), Is.EqualTo("Activate"));
                Assert.That(ChangeLogHelper.GetOperationName((ChangeLogOperation)99), Is.EqualTo("99"));
            });
        }

        [Test]
        public async Task DefaultInit_ProvidesNoOpCallbacks()
        {
            DefaultInit.DoNothing(null, "title", "message", true);
            await DefaultInit.DoNothing();
            await DefaultInit.DoNothing("text");
            await DefaultInit.DoNothing((WfStatefulObject)null!);
            await DefaultInit.DoNothing((WfTicket)null!);
            await DefaultInit.DoNothing((WfReqTask)null!);
            await DefaultInit.DoNothing((WfImplTask)null!);
            await DefaultInit.DoNothing((UiUser)null!);
            await DefaultInit.DoNothing((FwoOwner)null!);
            await DefaultInit.DoNothing((Device)null!);
            await DefaultInit.DoNothing((ComplianceCriterion)null!);

            Assert.Multiple(() =>
            {
                Assert.That(DefaultInit.DoNothingSync(), Is.False);
                Assert.That(DefaultInit.DoNothingSync((FWO.Data.Modelling.ModellingNwGroup)null!), Is.False);
                Assert.That(DefaultInit.DoNothingSync((FwoOwner)null!), Is.False);
            });
        }

        [Test]
        public void ComplianceViolationComparer_UsesAllIdentityFields()
        {
            ComplianceViolationComparer comparer = new();
            TestComplianceViolation first = new(1, 2, 3, "details");
            TestComplianceViolation same = new(1, 2, 3, "details");
            TestComplianceViolation different = new(1, 2, 3, "other");
            int firstHashCode = comparer.GetHashCode(first);
            int sameHashCode = comparer.GetHashCode(same);

            Assert.Multiple(() =>
            {
                Assert.That(comparer.Equals(first, first), Is.True);
                Assert.That(comparer.Equals(first, null), Is.False);
                Assert.That(comparer.Equals(first, same), Is.True);
                Assert.That(comparer.Equals(first, different), Is.False);
                Assert.That(firstHashCode, Is.EqualTo(sameHashCode));
            });
        }

        [Test]
        public void EnvironmentException_PreservesMessageAndInnerException()
        {
            InvalidOperationException innerException = new("inner");
            EnvironmentException exception = new("environment", innerException);

            Assert.Multiple(() =>
            {
                Assert.That(new EnvironmentException("message").Message, Is.EqualTo("message"));
                Assert.That(exception.Message, Is.EqualTo("environment"));
                Assert.That(exception.InnerException, Is.SameAs(innerException));
            });
        }

        [Test]
        public async Task ImportChangesNotifier_PreventsConcurrentExecutionAndResetsState()
        {
            TestNotifier notifier = new();
            Task<bool> firstRun = notifier.Run();

            Assert.That(await notifier.Run(), Is.False);
            notifier.Complete(true);
            Assert.That(await firstRun, Is.True);
            Assert.That(await notifier.Run(), Is.True);
        }

        [Test]
        public async Task AnonymousGlobalConfigTokenProvider_ValidatesResponsesAndDisposal()
        {
            TokenPair expectedTokenPair = new()
            {
                AccessToken = "anonymous-token",
                AccessTokenExpires = DateTime.UtcNow.AddMinutes(10)
            };
            await using MiddlewareClientTest.LocalMiddlewareServer server = new();
            using AnonymousGlobalConfigTokenProvider provider = new(server.BaseUrl);

            server.EnqueueResponse(JsonSerializer.Serialize(expectedTokenPair));
            TokenPair tokenPair = await provider.CreateTokenPairAsync(CancellationToken.None);

            Assert.That(tokenPair.AccessToken, Is.EqualTo(expectedTokenPair.AccessToken));
            server.EnqueueResponse("{}");
            Assert.ThrowsAsync<InvalidOperationException>(async () => await provider.CreateTokenPairAsync(CancellationToken.None));

            provider.Dispose();

            Assert.ThrowsAsync<ObjectDisposedException>(async () => await provider.CreateTokenPairAsync(CancellationToken.None));
        }

        private sealed class TestNotifier : FWImportChangesNotifierBase<AppServerImportEventArgs>
        {
            private TaskCompletionSource<bool>? completionSource;

            public void Complete(bool value)
            {
                completionSource!.SetResult(value);
            }

            protected override Task<bool> Execute(AppServerImportEventArgs? eventArgs = null)
            {
                completionSource ??= new TaskCompletionSource<bool>();
                return completionSource.Task;
            }
        }

        private sealed class TestComplianceViolation : IComplianceViolation
        {
            public TestComplianceViolation(int ruleId, int policyId, int criterionId, string details)
            {
                RuleId = ruleId;
                PolicyId = policyId;
                CriterionId = criterionId;
                Details = details;
            }

            public int RuleId { get; set; }
            public DateTime FoundDate { get; set; }
            public DateTime? RemovedDate { get; set; }
            public string Details { get; set; }
            public long RiskScore { get; set; }
            public int PolicyId { get; set; }
            public int CriterionId { get; set; }
        }
    }
}
