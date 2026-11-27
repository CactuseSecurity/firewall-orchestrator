using FWO.Data.DeviceSpecificSettings;
using FWO.Data.RulePositioning.CheckPoint;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests which tasks the provisioning work list contains, in which order, and which entry is provisioned next.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class ProvisioningWorkListTest
    {
        private const int kAutomaticManagementId = 1;
        private const int kExternalManagementId = 2;
        private const int kManualManagementId = 3;
        private const int kExternalWithoutSystemManagementId = 4;
        private const int kGatewayWithoutRuleWritesId = 5;
        private const int kOpenStateId = 210;
        private const int kEndStateId = 249;
        private const int kDoneStateId = 631;
        private const int kPlannedStateId = 149;
        private const int kBeingPlannedStateId = 110;

        private static readonly List<ProvisioningWay> kExternalTicketSystemOnly = [ProvisioningWay.ExternalTicketSystem];
        private static readonly List<long> kExampleReqTaskIds = [1, 2, 3];
        private static readonly List<ProvisioningWay> kExampleWays =
            [ProvisioningWay.FirewallWrite, ProvisioningWay.ExternalTicketSystem, ProvisioningWay.FirewallWrite];
        private static readonly List<bool> kExampleFinished = [false, false, true];
        private static readonly List<long?> kOrderedImplTaskIds = [11, 12, 13];
        private static readonly List<long?> kFirstImplTask = [11];
        private static readonly List<long?> kSecondImplTask = [12];
        private static readonly List<long?> kFirstAndSecondImplTask = [11, 12];
        private static readonly List<bool> kOnlyFirstReleased = [true, false];
        private static readonly HashSet<long> kReleasedFirstReqTask = [1];
        private static readonly CheckPointRulePosition kPosition =
            new("layer-uid", CheckPointPositionAnchorKind.Bottom, null, ProvisioningPositioningAlgorithm.DefaultEndOfRulebase);

        /// <summary>
        /// Settings for the tests: one management per implementation mode, rules and objects alike.
        /// </summary>
        private sealed class FixedModes(int? planningEndState = null) : IProvisioningModeSource
        {
            private static readonly List<int> kNoGateways = [];

            /// <summary>Gateways the rules of an implementation task take effect on, by implementation task id.</summary>
            public Dictionary<long, List<int>> EnforcingGateways { get; } = [];

            public IReadOnlyList<int> GetRuleEnforcingGateways(WfImplTask implTask)
            {
                return EnforcingGateways.TryGetValue(implTask.Id, out List<int>? gatewayIds) ? gatewayIds : kNoGateways;
            }

            /// <summary>Why a rule cannot be written automatically, by implementation task id.</summary>
            public Dictionary<long, string> NotWritableReasons { get; } = [];

            public string? GetRuleNotWritableReason(WfImplTask implTask)
            {
                return NotWritableReasons.TryGetValue(implTask.Id, out string? reason) ? reason : null;
            }

            /// <summary>
            /// Target managements by implementation task id, as the shared resolver gives them; null for a task whose
            /// management cannot be resolved consistently. Other tasks target their own management, else the one of the
            /// request task.
            /// </summary>
            public Dictionary<long, int?> TargetManagements { get; } = [];

            public int? GetTargetManagement(WfReqTask reqTask, WfImplTask implTask)
            {
                return TargetManagements.TryGetValue(implTask.Id, out int? managementId) ? managementId : implTask.ManagementId ?? reqTask.ManagementId;
            }

            public ProvisioningImplementationMode GetImplementationMode(int managementId, bool isRuleTask)
            {
                return managementId switch
                {
                    kExternalManagementId or kExternalWithoutSystemManagementId => ProvisioningImplementationMode.RerouteToExternalTicketSystem,
                    kManualManagementId => ProvisioningImplementationMode.Manual,
                    _ => ProvisioningImplementationMode.Automatic
                };
            }

            /// <summary>Gateways take the mode of the management with the same id.</summary>
            public ProvisioningImplementationMode GetRuleImplementationMode(int gatewayId)
            {
                return GetImplementationMode(gatewayId, true);
            }

            public bool WritesRules(int gatewayId)
            {
                return gatewayId != kGatewayWithoutRuleWritesId;
            }

            /// <summary>Gateways whose vendor writes new rules without position (Guardicore).</summary>
            public HashSet<int> GatewaysWithoutPositioning { get; } = [];

            public bool NewRulesNeedPosition(int gatewayId)
            {
                return !GatewaysWithoutPositioning.Contains(gatewayId);
            }

            public ExternalTicketSystemKind GetExternalTicketSystem(int managementId)
            {
                return managementId == kExternalManagementId ? ExternalTicketSystemKind.TufinSecureChange : ExternalTicketSystemKind.Undefined;
            }

            public int GetImplementationEndState(string taskType)
            {
                return kEndStateId;
            }

            public int? GetPlanningEndState(string taskType)
            {
                return planningEndState;
            }
        }

        private static WfReqTask ReqTask(long id, int taskNumber, int? managementId, string taskType = nameof(WfTaskType.group_create), int stateId = kOpenStateId)
        {
            return new WfReqTask { Id = id, TicketId = 5, TaskNumber = taskNumber, TaskType = taskType, ManagementId = managementId, StateId = stateId };
        }

        private static WfImplTask AddImplTask(WfReqTask reqTask, long id, int taskNumber, int? managementId, int stateId = kOpenStateId)
        {
            WfImplTask implTask = new() { Id = id, ReqTaskId = reqTask.Id, TaskNumber = taskNumber, TaskType = reqTask.TaskType, ManagementId = managementId, StateId = stateId };
            reqTask.ImplementationTasks.Add(implTask);
            return implTask;
        }

        /// <summary>Adds a rule implementation task on a gateway, with a calculated position unless stated otherwise.</summary>
        private static WfImplTask AddRuleImplTask(WfReqTask reqTask, long id, int gatewayId, bool positioned = true, int stateId = kOpenStateId)
        {
            WfImplTask implTask = AddImplTask(reqTask, id, reqTask.ImplementationTasks.Count + 1, kAutomaticManagementId, stateId);
            implTask.DeviceId = gatewayId;
            implTask.RulePosition = positioned ? kPosition : null;
            return implTask;
        }

        private static ProvisioningWorkList Build(params WfReqTask[] reqTasks)
        {
            return ProvisioningWorkList.Build(reqTasks.ToList(), new FixedModes());
        }

        /// <summary>Builds the work list with the given settings.</summary>
        private static ProvisioningWorkList BuildWith(FixedModes modes, params WfReqTask[] reqTasks)
        {
            return ProvisioningWorkList.Build(reqTasks.ToList(), modes);
        }

        /// <summary>Builds the work list with an active planning phase ending in the planned state.</summary>
        private static ProvisioningWorkList BuildWithPlanning(params WfReqTask[] reqTasks)
        {
            return ProvisioningWorkList.Build(reqTasks.ToList(), new FixedModes(kPlannedStateId));
        }

        /// <summary>Builds the work list with the request task 1 released by a button.</summary>
        private static ProvisioningWorkList BuildReleasingTheFirstRequestTask(params WfReqTask[] reqTasks)
        {
            return ProvisioningWorkList.Build(reqTasks.ToList(), new FixedModes(), kReleasedFirstReqTask);
        }

        private static List<long?> ImplTaskIds(ProvisioningWorkList workList)
        {
            return [.. workList.Items.Select(item => item.ImplTask?.Id)];
        }

        [Test]
        public void Build_TheExampleTicket_ProvisionsInOrderAndSkipsManualAndFinishedTasks()
        {
            WfReqTask first = ReqTask(1, 1, kAutomaticManagementId);
            AddImplTask(first, 11, 1, kAutomaticManagementId);
            AddImplTask(first, 12, 2, kManualManagementId);
            WfReqTask second = ReqTask(2, 2, kExternalManagementId);
            WfReqTask third = ReqTask(3, 3, kAutomaticManagementId, nameof(WfTaskType.group_modify));
            AddImplTask(third, 31, 1, kAutomaticManagementId, kDoneStateId);

            ProvisioningWorkList workList = Build(third, second, first);

            List<ProvisioningWorkItem> items = workList.Items;
            Assert.Multiple(() =>
            {
                Assert.That(items.Select(item => item.ReqTask.Id), Is.EqualTo(kExampleReqTaskIds));
                Assert.That(items.Select(item => item.Way), Is.EqualTo(kExampleWays));
                Assert.That(items.Select(item => item.IsFinished), Is.EqualTo(kExampleFinished));
                Assert.That(workList.Next?.ImplTask?.Id, Is.EqualTo(11));
                Assert.That(workList.Gaps, Is.Empty);
            });
        }

        [Test]
        public void Build_ImplementationTasksAreOrderedByTheirNumber()
        {
            WfReqTask reqTask = ReqTask(1, 1, kAutomaticManagementId);
            AddImplTask(reqTask, 13, 3, kAutomaticManagementId);
            AddImplTask(reqTask, 11, 1, kAutomaticManagementId);
            AddImplTask(reqTask, 12, 2, kAutomaticManagementId);

            ProvisioningWorkList workList = Build(reqTask);

            Assert.That(ImplTaskIds(workList), Is.EqualTo(kOrderedImplTaskIds));
        }

        [Test]
        public void Build_RequestTaskOfAnExternalManagement_IsHandedOverAsAWhole()
        {
            WfReqTask reqTask = ReqTask(1, 1, kExternalManagementId);
            AddImplTask(reqTask, 11, 1, kAutomaticManagementId);

            ProvisioningWorkList workList = Build(reqTask);

            Assert.Multiple(() =>
            {
                Assert.That(workList.Items.Select(item => item.Way), Is.EqualTo(kExternalTicketSystemOnly));
                Assert.That(workList.Items[0].ImplTask, Is.Null);
                Assert.That(workList.Items[0].ExternalTicketSystem, Is.EqualTo(ExternalTicketSystemKind.TufinSecureChange));
            });
        }

        [Test]
        public void Build_RuleTaskOfAnExternalManagement_IsHandedOverAsAWhole()
        {
            WfReqTask reqTask = ReqTask(1, 1, kExternalManagementId, nameof(WfTaskType.access));

            ProvisioningWorkList workList = Build(reqTask);

            Assert.That(workList.Items.Select(item => item.Way), Is.EqualTo(kExternalTicketSystemOnly));
        }

        [Test]
        public void Build_ExternalManagementWithoutSystem_IsReportedAndNotProvisioned()
        {
            WfReqTask reqTask = ReqTask(1, 1, kExternalWithoutSystemManagementId);
            AddImplTask(reqTask, 11, 1, kAutomaticManagementId);

            ProvisioningWorkList workList = Build(reqTask);

            Assert.Multiple(() =>
            {
                Assert.That(workList.Items, Is.Empty);
                Assert.That(workList.Gaps, Has.Count.EqualTo(1));
                Assert.That(workList.Gaps[0].ImplTask, Is.Null);
                Assert.That(workList.Gaps[0].ManagementId, Is.EqualTo(kExternalWithoutSystemManagementId));
                Assert.That(workList.Gaps[0].Reason, Does.Contain("no external ticket system is selected"));
            });
        }

        [Test]
        public void Build_FinishedRequestTaskOfAnExternalManagementWithoutSystem_IsNotReported()
        {
            WfReqTask reqTask = ReqTask(1, 1, kExternalWithoutSystemManagementId, stateId: kDoneStateId);

            ProvisioningWorkList workList = Build(reqTask);

            Assert.That(workList.Gaps, Is.Empty);
        }

        [Test]
        public void Build_FinishedRequestTaskOfAnExternalManagement_IsFinished()
        {
            WfReqTask reqTask = ReqTask(1, 1, kExternalManagementId, stateId: kEndStateId);

            ProvisioningWorkList workList = Build(reqTask);

            Assert.Multiple(() =>
            {
                Assert.That(workList.Items[0].IsFinished, Is.True);
                Assert.That(workList.Next, Is.Null);
            });
        }

        [Test]
        public void Build_ImplementationTaskOnAnExternalTarget_IsReportedAndNotProvisioned()
        {
            WfReqTask reqTask = ReqTask(1, 1, kAutomaticManagementId);
            AddImplTask(reqTask, 11, 1, kExternalManagementId);

            ProvisioningWorkList workList = Build(reqTask);

            Assert.Multiple(() =>
            {
                Assert.That(workList.Items, Is.Empty);
                Assert.That(workList.Gaps, Has.Count.EqualTo(1));
                Assert.That(workList.Gaps[0].ImplTask?.Id, Is.EqualTo(11));
                Assert.That(workList.Gaps[0].ManagementId, Is.EqualTo(kExternalManagementId));
            });
        }

        [Test]
        public void Build_FinishedImplementationTaskOnAnExternalTarget_IsNotReported()
        {
            WfReqTask reqTask = ReqTask(1, 1, kAutomaticManagementId);
            AddImplTask(reqTask, 11, 1, kExternalManagementId, kDoneStateId);

            ProvisioningWorkList workList = Build(reqTask);

            Assert.That(workList.Gaps, Is.Empty);
        }

        [Test]
        public void Build_ImplementationTaskOnAManualTarget_IsLeftToAPerson()
        {
            WfReqTask reqTask = ReqTask(1, 1, kAutomaticManagementId);
            AddImplTask(reqTask, 11, 1, kManualManagementId);
            AddImplTask(reqTask, 12, 2, kAutomaticManagementId);

            ProvisioningWorkList workList = Build(reqTask);

            Assert.Multiple(() =>
            {
                Assert.That(ImplTaskIds(workList), Is.EqualTo(kSecondImplTask));
                Assert.That(workList.Gaps, Is.Empty);
            });
        }

        [Test]
        public void Build_ReleasedByAButton_ManualTargetIsWritten()
        {
            WfReqTask reqTask = ReqTask(1, 1, kAutomaticManagementId);
            AddImplTask(reqTask, 11, 1, kManualManagementId);
            AddImplTask(reqTask, 12, 2, kAutomaticManagementId);

            ProvisioningWorkList workList = BuildReleasingTheFirstRequestTask(reqTask);

            Assert.Multiple(() =>
            {
                Assert.That(ImplTaskIds(workList), Is.EqualTo(kFirstAndSecondImplTask));
                Assert.That(workList.Items.Select(item => item.Way), Is.All.EqualTo(ProvisioningWay.FirewallWrite));
                Assert.That(workList.Items.Select(item => item.ReleasedByButton), Is.EqualTo(kOnlyFirstReleased));
                Assert.That(workList.Next?.ImplTask?.Id, Is.EqualTo(11));
            });
        }

        [Test]
        public void Build_ReleasedByAButton_ManualTargetOfAnotherRequestTaskIsLeftToAPerson()
        {
            WfReqTask released = ReqTask(1, 1, kAutomaticManagementId);
            AddImplTask(released, 11, 1, kManualManagementId);
            WfReqTask other = ReqTask(2, 2, kAutomaticManagementId);
            AddImplTask(other, 21, 1, kManualManagementId);

            ProvisioningWorkList workList = BuildReleasingTheFirstRequestTask(released, other);

            Assert.That(ImplTaskIds(workList), Is.EqualTo(kFirstImplTask));
        }

        [Test]
        public void Build_ReleasedByAButton_ExternalTargetStaysAGap()
        {
            WfReqTask reqTask = ReqTask(1, 1, kAutomaticManagementId);
            AddImplTask(reqTask, 11, 1, kExternalManagementId);

            ProvisioningWorkList workList = BuildReleasingTheFirstRequestTask(reqTask);

            Assert.Multiple(() =>
            {
                Assert.That(workList.Items, Is.Empty);
                Assert.That(workList.Gaps, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public void Build_ImplementationTaskWithoutManagement_IsNotProvisioned()
        {
            WfReqTask reqTask = ReqTask(1, 1, kAutomaticManagementId);
            AddImplTask(reqTask, 11, 1, null);

            ProvisioningWorkList workList = Build(reqTask);

            Assert.That(workList.Items, Is.Empty);
        }

        [Test]
        public void Build_NewRuleWithPositionOnAnAutomaticGateway_IsWritten()
        {
            WfReqTask reqTask = ReqTask(1, 1, kAutomaticManagementId, nameof(WfTaskType.access));
            AddRuleImplTask(reqTask, 11, kAutomaticManagementId);

            ProvisioningWorkList workList = Build(reqTask);

            Assert.Multiple(() =>
            {
                Assert.That(ImplTaskIds(workList), Is.EqualTo(kFirstImplTask));
                Assert.That(workList.Items[0].Way, Is.EqualTo(ProvisioningWay.FirewallWrite));
                Assert.That(workList.Gaps, Is.Empty);
            });
        }

        [Test]
        public void Build_NewRuleWithoutPosition_IsReportedAndNotWritten()
        {
            WfReqTask reqTask = ReqTask(1, 1, kAutomaticManagementId, nameof(WfTaskType.access));
            AddRuleImplTask(reqTask, 11, kAutomaticManagementId, false);

            ProvisioningWorkList workList = Build(reqTask);

            Assert.Multiple(() =>
            {
                Assert.That(workList.Items, Is.Empty);
                Assert.That(workList.Gaps, Has.Count.EqualTo(1));
                Assert.That(workList.Gaps[0].Reason, Does.Contain("no rule position"));
            });
        }

        /// <summary>Guardicore writes new rules to its Allow section, where the order does not matter: no position is needed.</summary>
        [Test]
        public void Build_NewRuleWithoutPositionForVendorWithoutPositioning_IsWritten()
        {
            WfReqTask reqTask = ReqTask(1, 1, kAutomaticManagementId, nameof(WfTaskType.access));
            AddRuleImplTask(reqTask, 11, kAutomaticManagementId, false);
            FixedModes modes = new();
            modes.GatewaysWithoutPositioning.Add(kAutomaticManagementId);
            List<WfReqTask> reqTasks = [reqTask];

            ProvisioningWorkList workList = ProvisioningWorkList.Build(reqTasks, modes);

            Assert.Multiple(() =>
            {
                Assert.That(workList.Gaps, Is.Empty);
                Assert.That(workList.Items, Has.Count.EqualTo(1));
            });
        }

        [TestCase(nameof(WfTaskType.access))]
        [TestCase(nameof(WfTaskType.rule_modify))]
        public void Build_RuleNotWritableAutomatically_IsReportedWithItsReason(string taskType)
        {
            const string kReason = "placeholder area NA_Users is used in source and destination";
            WfReqTask reqTask = ReqTask(1, 1, kAutomaticManagementId, taskType);
            AddRuleImplTask(reqTask, 11, kAutomaticManagementId);
            AddRuleImplTask(reqTask, 12, kAutomaticManagementId);
            FixedModes modes = new();
            modes.NotWritableReasons[11] = kReason;

            ProvisioningWorkList workList = BuildWith(modes, reqTask);

            Assert.Multiple(() =>
            {
                Assert.That(ImplTaskIds(workList), Is.EqualTo(kSecondImplTask));
                Assert.That(workList.Gaps, Has.Count.EqualTo(1));
                Assert.That(workList.Gaps[0].ImplTask?.Id, Is.EqualTo(11));
                Assert.That(workList.Gaps[0].Reason, Is.EqualTo(kReason));
            });
        }

        [Test]
        public void Build_FinishedRuleNotWritableAutomatically_IsNotReported()
        {
            WfReqTask reqTask = ReqTask(1, 1, kAutomaticManagementId, nameof(WfTaskType.access));
            AddRuleImplTask(reqTask, 11, kAutomaticManagementId, stateId: kDoneStateId);
            FixedModes modes = new();
            modes.NotWritableReasons[11] = "placeholder";

            ProvisioningWorkList workList = BuildWith(modes, reqTask);

            Assert.That(workList.Gaps, Is.Empty);
        }

        [TestCase(nameof(WfTaskType.rule_modify))]
        [TestCase(nameof(WfTaskType.rule_delete))]
        public void Build_ChangedOrDeletedRule_IsWrittenWithoutPosition(string taskType)
        {
            WfReqTask reqTask = ReqTask(1, 1, kAutomaticManagementId, taskType);
            AddRuleImplTask(reqTask, 11, kAutomaticManagementId, false);

            ProvisioningWorkList workList = Build(reqTask);

            Assert.That(ImplTaskIds(workList), Is.EqualTo(kFirstImplTask));
        }

        [Test]
        public void Build_RuleOnAVendorWithoutRuleWrites_IsReportedAndNotWritten()
        {
            WfReqTask reqTask = ReqTask(1, 1, kAutomaticManagementId, nameof(WfTaskType.rule_modify));
            AddRuleImplTask(reqTask, 11, kGatewayWithoutRuleWritesId);

            ProvisioningWorkList workList = Build(reqTask);

            Assert.Multiple(() =>
            {
                Assert.That(workList.Items, Is.Empty);
                Assert.That(workList.Gaps, Has.Count.EqualTo(1));
                Assert.That(workList.Gaps[0].Reason, Does.Contain("not written to the vendor"));
            });
        }

        [Test]
        public void Build_FinishedRuleWithoutPosition_IsNotReported()
        {
            WfReqTask reqTask = ReqTask(1, 1, kAutomaticManagementId, nameof(WfTaskType.access));
            AddRuleImplTask(reqTask, 11, kAutomaticManagementId, false, kDoneStateId);

            ProvisioningWorkList workList = Build(reqTask);

            Assert.Multiple(() =>
            {
                Assert.That(workList.Gaps, Is.Empty);
                Assert.That(workList.Next, Is.Null);
            });
        }

        [Test]
        public void Build_RuleOnAManualGateway_IsWrittenOnlyWhenReleased()
        {
            WfReqTask reqTask = ReqTask(1, 1, kAutomaticManagementId, nameof(WfTaskType.access));
            AddRuleImplTask(reqTask, 11, kManualManagementId);

            ProvisioningWorkList workList = Build(reqTask);
            ProvisioningWorkList released = BuildReleasingTheFirstRequestTask(reqTask);

            Assert.Multiple(() =>
            {
                Assert.That(workList.Items, Is.Empty);
                Assert.That(workList.Gaps, Is.Empty);
                Assert.That(ImplTaskIds(released), Is.EqualTo(kFirstImplTask));
                Assert.That(released.Items[0].ReleasedByButton, Is.True);
            });
        }

        [Test]
        public void Build_RuleOnAnExternalGateway_IsReported()
        {
            WfReqTask reqTask = ReqTask(1, 1, kAutomaticManagementId, nameof(WfTaskType.access));
            AddRuleImplTask(reqTask, 11, kExternalManagementId);

            ProvisioningWorkList workList = Build(reqTask);

            Assert.Multiple(() =>
            {
                Assert.That(workList.Items, Is.Empty);
                Assert.That(workList.Gaps, Has.Count.EqualTo(1));
                Assert.That(workList.Gaps[0].Reason, Does.Contain("external ticket system"));
            });
        }

        [Test]
        public void Build_RuleWithoutGateway_IsLeftToAPerson()
        {
            WfReqTask reqTask = ReqTask(1, 1, kAutomaticManagementId, nameof(WfTaskType.access));
            AddImplTask(reqTask, 11, 1, kAutomaticManagementId);

            ProvisioningWorkList workList = Build(reqTask);

            Assert.Multiple(() =>
            {
                Assert.That(workList.Items, Is.Empty);
                Assert.That(workList.Gaps, Is.Empty);
            });
        }

        [TestCase(nameof(WfTaskType.new_interface))]
        [TestCase(nameof(WfTaskType.generic))]
        public void Build_TaskTypeWithoutTarget_IsNeverProvisioned(string taskType)
        {
            WfReqTask reqTask = ReqTask(1, 1, kExternalManagementId, taskType);
            AddImplTask(reqTask, 11, 1, kAutomaticManagementId);

            ProvisioningWorkList workList = Build(reqTask);

            Assert.That(workList.Items, Is.Empty);
        }

        [Test]
        public void Build_ObjectTaskWithoutManagement_ProvisionsItsImplementationTasks()
        {
            WfReqTask reqTask = ReqTask(1, 1, null, nameof(WfTaskType.object_create));
            AddImplTask(reqTask, 11, 1, kAutomaticManagementId);

            ProvisioningWorkList workList = Build(reqTask);

            Assert.That(ImplTaskIds(workList), Is.EqualTo(kFirstImplTask));
        }

        [Test]
        public void Next_SkipsFinishedEntries()
        {
            WfReqTask reqTask = ReqTask(1, 1, kAutomaticManagementId);
            AddImplTask(reqTask, 11, 1, kAutomaticManagementId, kEndStateId);
            AddImplTask(reqTask, 12, 2, kAutomaticManagementId);

            ProvisioningWorkList workList = Build(reqTask);

            Assert.That(workList.Next?.ImplTask?.Id, Is.EqualTo(12));
        }

        [Test]
        public void Build_PlanningActive_RequestTaskBeingPlannedHoldsBackTheEntriesBehindIt()
        {
            WfReqTask beingPlanned = ReqTask(1, 1, kAutomaticManagementId, stateId: kBeingPlannedStateId);
            AddImplTask(beingPlanned, 11, 1, kAutomaticManagementId);
            WfReqTask planned = ReqTask(2, 2, kAutomaticManagementId, stateId: kPlannedStateId);
            AddImplTask(planned, 21, 1, kAutomaticManagementId);

            ProvisioningWorkList workList = BuildWithPlanning(beingPlanned, planned);

            Assert.Multiple(() =>
            {
                Assert.That(workList.Next?.ImplTask?.Id, Is.EqualTo(11));
                Assert.That(workList.Next?.WaitsForPlanning, Is.True);
                Assert.That(workList.Items[1].WaitsForPlanning, Is.False);
            });
        }

        [Test]
        public void Build_PlanningActive_RequestTaskOfAnExternalManagementIsHandedOverBeforeItIsPlanned()
        {
            WfReqTask reqTask = ReqTask(1, 1, kExternalManagementId, stateId: kBeingPlannedStateId);

            ProvisioningWorkList workList = BuildWithPlanning(reqTask);

            Assert.That(workList.Next?.WaitsForPlanning, Is.False);
        }

        [Test]
        public void Build_PlanningInactive_NothingWaitsForPlanning()
        {
            WfReqTask reqTask = ReqTask(1, 1, kAutomaticManagementId, stateId: kBeingPlannedStateId);
            AddImplTask(reqTask, 11, 1, kAutomaticManagementId);

            ProvisioningWorkList workList = Build(reqTask);

            Assert.That(workList.Next?.WaitsForPlanning, Is.False);
        }

        /// <summary>Builds the work list of one rule_delete task on an automatic gateway whose rule takes effect on further gateways.</summary>
        private static (ProvisioningWorkList WorkList, WfImplTask ImplTask) BuildRuleDeleteEnforcedOn(List<int> enforcingGatewayIds, bool released = false)
        {
            WfReqTask reqTask = ReqTask(1, 1, kAutomaticManagementId, nameof(WfTaskType.rule_delete));
            WfImplTask implTask = AddRuleImplTask(reqTask, 11, kAutomaticManagementId, positioned: false);
            FixedModes modes = new();
            modes.EnforcingGateways[implTask.Id] = enforcingGatewayIds;
            List<WfReqTask> reqTasks = [reqTask];
            ProvisioningWorkList workList = released
                ? ProvisioningWorkList.Build(reqTasks, modes, kReleasedFirstReqTask)
                : ProvisioningWorkList.Build(reqTasks, modes);
            return (workList, implTask);
        }

        private static readonly List<int> kEnforcedOnAutomaticGateways = [kAutomaticManagementId];
        private static readonly List<int> kEnforcedOnManualGateway = [kAutomaticManagementId, kManualManagementId];
        private static readonly List<int> kEnforcedOnExternalGateway = [kManualManagementId, kExternalManagementId];

        [Test]
        public void Build_ExistingRuleOnAutomaticGatewaysOnly_IsWritten()
        {
            (ProvisioningWorkList workList, WfImplTask implTask) = BuildRuleDeleteEnforcedOn(kEnforcedOnAutomaticGateways);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(workList.Items.Single().ImplTask, Is.SameAs(implTask));
                Assert.That(workList.Gaps, Is.Empty);
            }
        }

        [Test]
        public void Build_ExistingRuleAlsoOnManualGateway_IsLeftToAPersonUnlessReleased()
        {
            (ProvisioningWorkList notReleased, _) = BuildRuleDeleteEnforcedOn(kEnforcedOnManualGateway);
            (ProvisioningWorkList released, _) = BuildRuleDeleteEnforcedOn(kEnforcedOnManualGateway, true);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(notReleased.Items, Is.Empty);
                Assert.That(notReleased.Gaps.Single().Reason, Does.StartWith($"the rule also takes effect on gateway {kManualManagementId}, which is set to manual"));
                Assert.That(released.Items.Single().ReleasedByButton, Is.True);
                Assert.That(released.Gaps, Is.Empty);
            }
        }

        [Test]
        public void Build_ExistingRuleAlsoOnExternalGateway_IsAGapNamingThatGateway()
        {
            (ProvisioningWorkList workList, _) = BuildRuleDeleteEnforcedOn(kEnforcedOnExternalGateway, true);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(workList.Items, Is.Empty);
                Assert.That(workList.Gaps.Single().Reason, Does.StartWith($"gateway {kExternalManagementId} is set to external ticket system"));
            }
        }

        private const int kSecondAutomaticGatewayId = 6;
        private const int kThirdAutomaticGatewayId = 7;
        private static readonly CheckPointRulePosition kOtherLayerPosition =
            new("other-layer-uid", CheckPointPositionAnchorKind.Bottom, null, ProvisioningPositioningAlgorithm.DefaultEndOfRulebase);
        private static readonly List<int> kSharedRuleGateways = [kAutomaticManagementId, kSecondAutomaticGatewayId];
        private static readonly List<int> kSecondTaskCovered = [2];
        private static readonly List<int> kSharedRuleWithReleasedManualGateway = [kAutomaticManagementId, kManualManagementId];

        private static WfReqTask NewRuleTask(long id, int taskNumber) => ReqTask(id, taskNumber, kAutomaticManagementId, nameof(WfTaskType.access));

        [Test]
        public void Build_NewRulesOfOneRequestTaskInTheSameLayer_AreWrittenOnce()
        {
            WfReqTask reqTask = NewRuleTask(1, 1);
            AddRuleImplTask(reqTask, 11, kAutomaticManagementId);
            AddRuleImplTask(reqTask, 12, kSecondAutomaticGatewayId);
            AddRuleImplTask(reqTask, 13, kThirdAutomaticGatewayId).RulePosition = kOtherLayerPosition;

            ProvisioningWorkList workList = Build(reqTask);

            SharedRule? sharedRule = workList.Items[0].Rule;
            using (Assert.EnterMultipleScope())
            {
                Assert.That(ImplTaskIds(workList), Is.EqualTo(kOrderedImplTaskIds));
                Assert.That(sharedRule?.WriterTaskNumber, Is.EqualTo(1));
                Assert.That(sharedRule?.InstallOnGatewayIds, Is.EqualTo(kSharedRuleGateways));
                Assert.That(sharedRule?.CoveredTaskNumbers, Is.EqualTo(kSecondTaskCovered));
                Assert.That(workList.Items[1].Rule, Is.SameAs(sharedRule));
                Assert.That(workList.Items[2].Rule, Is.Null);
            }
        }

        [Test]
        public void Build_NewRulesOfDifferentRequestTasks_AreWrittenSeparately()
        {
            WfReqTask first = NewRuleTask(1, 1);
            AddRuleImplTask(first, 11, kAutomaticManagementId);
            WfReqTask second = NewRuleTask(2, 2);
            AddRuleImplTask(second, 12, kSecondAutomaticGatewayId);

            ProvisioningWorkList workList = Build(first, second);

            Assert.That(workList.Items.Select(item => item.Rule), Is.All.Null);
        }

        [Test]
        public void Build_ManualTaskInTheSameLayer_JoinsTheSharedRuleOnlyWhenReleased()
        {
            WfReqTask reqTask = NewRuleTask(1, 1);
            AddRuleImplTask(reqTask, 11, kAutomaticManagementId);
            AddRuleImplTask(reqTask, 12, kManualManagementId);

            ProvisioningWorkList notReleased = Build(reqTask);
            ProvisioningWorkList released = BuildReleasingTheFirstRequestTask(reqTask);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(notReleased.Items.Single().Rule, Is.Null);
                Assert.That(released.Items[0].Rule?.InstallOnGatewayIds, Is.EqualTo(kSharedRuleWithReleasedManualGateway));
            }
        }

        [Test]
        public void Build_ChangedRulesOfOneRequestTask_AreNotShared()
        {
            WfReqTask reqTask = ReqTask(1, 1, kAutomaticManagementId, nameof(WfTaskType.rule_modify));
            AddRuleImplTask(reqTask, 11, kAutomaticManagementId);
            AddRuleImplTask(reqTask, 12, kSecondAutomaticGatewayId);

            ProvisioningWorkList workList = Build(reqTask);

            Assert.That(workList.Items.Select(item => item.Rule), Is.All.Null);
        }

        [Test]
        public void Build_GatewayTaskWithInconsistentManagement_IsReportedAndNotWritten()
        {
            WfReqTask reqTask = NewRuleTask(1, 1);
            AddRuleImplTask(reqTask, 11, kAutomaticManagementId);
            AddRuleImplTask(reqTask, 12, kSecondAutomaticGatewayId, stateId: kDoneStateId);
            FixedModes modes = new();
            modes.TargetManagements[11] = null;
            modes.TargetManagements[12] = null;

            List<WfReqTask> reqTasks = [reqTask];
            ProvisioningWorkList workList = ProvisioningWorkList.Build(reqTasks, modes);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(workList.Items, Is.Empty);
                Assert.That(workList.Gaps.Single().ImplTask?.Id, Is.EqualTo(11), "a finished task is not reported");
                Assert.That(workList.Gaps.Single().Reason, Does.Contain($"does not match the management of its gateway {kAutomaticManagementId}"));
            }
        }

        [Test]
        public void Build_GatewayTaskWithoutManagementAndUnknownGateway_IsReported()
        {
            WfReqTask reqTask = NewRuleTask(1, 1);
            WfImplTask implTask = AddRuleImplTask(reqTask, 11, kAutomaticManagementId);
            implTask.ManagementId = null;
            FixedModes modes = new();
            modes.TargetManagements[11] = null;

            List<WfReqTask> reqTasks = [reqTask];
            ProvisioningWorkList workList = ProvisioningWorkList.Build(reqTasks, modes);

            Assert.That(workList.Gaps.Single().Reason, Does.Contain($"its gateway {kAutomaticManagementId} is not known or has no management"));
        }

        /// <summary>
        /// A rule task stored without management belongs to the management of its gateway, not to the one of its request
        /// task, so it shares the rule with the other tasks on that management.
        /// </summary>
        [Test]
        public void Build_TaskWithoutStoredManagement_SharesTheRuleOnTheManagementOfItsGateway()
        {
            const int kOtherRequestManagementId = 9;
            WfReqTask reqTask = ReqTask(1, 1, kOtherRequestManagementId, nameof(WfTaskType.access));
            WfImplTask withoutManagement = AddRuleImplTask(reqTask, 11, kAutomaticManagementId);
            withoutManagement.ManagementId = null;
            AddRuleImplTask(reqTask, 12, kSecondAutomaticGatewayId);
            FixedModes modes = new();
            modes.TargetManagements[11] = kAutomaticManagementId;

            List<WfReqTask> reqTasks = [reqTask];
            ProvisioningWorkList workList = ProvisioningWorkList.Build(reqTasks, modes);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(workList.Items.Select(item => item.Rule?.WriterTaskNumber), Is.All.EqualTo(1));
                Assert.That(workList.Gaps, Is.Empty);
            }
        }

        [Test]
        public void Build_ObjectTaskOnAGatewayWithInconsistentManagement_IsReported()
        {
            WfReqTask reqTask = ReqTask(1, 1, kAutomaticManagementId);
            WfImplTask implTask = AddImplTask(reqTask, 11, 1, kAutomaticManagementId);
            implTask.DeviceId = kSecondAutomaticGatewayId;
            FixedModes modes = new();
            modes.TargetManagements[11] = null;

            List<WfReqTask> reqTasks = [reqTask];
            ProvisioningWorkList workList = ProvisioningWorkList.Build(reqTasks, modes);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(workList.Items, Is.Empty);
                Assert.That(workList.Gaps.Single().ImplTask?.Id, Is.EqualTo(11));
            }
        }

        [Test]
        public void Next_IsNullWhenEverythingIsFinished()
        {
            WfReqTask reqTask = ReqTask(1, 1, kAutomaticManagementId);
            AddImplTask(reqTask, 11, 1, kAutomaticManagementId, kDoneStateId);

            ProvisioningWorkList workList = Build(reqTask);

            Assert.That(workList.Next, Is.Null);
        }
    }
}
