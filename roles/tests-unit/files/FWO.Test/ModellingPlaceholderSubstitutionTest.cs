using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.ExternalSystems.FirewallWrite;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests the substitution of modelling placeholder areas by the objects the extra configurations of the connection
    /// name, and the cases in which a rule is not written automatically.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class ModellingPlaceholderSubstitutionTest
    {
        private const string kUserArea = "NA_Users";
        private const string kSecondUserArea = "NA_Users2";
        private const string kUpdatableArea = "NA_Updatable";
        private const string kOtherArea = "NA_Server";
        private const string kUserType = "IDA-User";
        private const string kUpdatableType = "Updatable Objects";

        private static readonly List<ModellingPlaceholderArea> kAreas =
        [
            new(kUserArea, ModellingPlaceholderKind.User, true, false),
            new(kSecondUserArea, ModellingPlaceholderKind.User, true, false),
            new(kUpdatableArea, ModellingPlaceholderKind.UpdatableObject, true, true)
        ];
        private static readonly List<ModellingExtraConfig> kNoExtraConfigs = [];
        private static readonly List<string> kTwoAccessRoles = ["a_testUser", "AR_Sales"];
        private static readonly List<string> kExchange = ["Exchange Online China Services"];
        private static readonly List<string> kServerArea = [kOtherArea];

        private static WfImplElement Group(string groupName, ElemFieldType field, RequestAction action = RequestAction.create) => new()
        {
            GroupName = groupName,
            Field = field.ToString(),
            ImplAction = action.ToString(),
            ImplTaskId = 7
        };

        private static WfImplElement Service() => new() { Name = "https", ProtoId = 6, Port = 443, Field = ElemFieldType.service.ToString() };

        private static WfImplTask Task(WfTaskType taskType, params WfImplElement[] elements) => new()
        {
            Id = 7,
            TaskType = taskType.ToString(),
            ImplElements = elements.ToList()
        };

        private static ModellingExtraConfig Config(string type, string text) => new() { ExtraConfigType = type, ExtraConfigText = text };

        private static ModellingPlaceholderInput Input(params ModellingExtraConfig[] extraConfigs) => new(kAreas, extraConfigs.ToList(), kNoExtraConfigs);

        private static List<string> NamesOn(WfImplTask task, ElemFieldType field) =>
            task.ImplElements.Where(element => element.Field == field.ToString() && element.GroupName != null).Select(element => element.GroupName!).ToList();

        [Test]
        public void Access_UserPlaceholder_IsReplacedByTheNamedObjects()
        {
            WfImplTask task = Task(WfTaskType.access, Group(kUserArea, ElemFieldType.source), Group(kOtherArea, ElemFieldType.destination), Service());

            ModellingPlaceholderResult result = ModellingPlaceholderSubstitution.Apply(task,
                Input(Config(kUserType, "a_testUser"), Config(kUserType, " AR_Sales "), Config(kUserType, "A_TESTUSER")));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.IsWritable, Is.True);
                Assert.That(NamesOn(result.ImplTask, ElemFieldType.source), Is.EqualTo(kTwoAccessRoles));
                Assert.That(result.ImplTask.ImplElements.Where(e => e.Field == ElemFieldType.source.ToString()).All(e => e.KeepNameVerbatim && e.ImplTaskId == 7), Is.True);
                Assert.That(NamesOn(result.ImplTask, ElemFieldType.destination), Is.EqualTo(kServerArea));
                Assert.That(result.ImplTask.ImplElements.Count(e => e.Field == ElemFieldType.service.ToString()), Is.EqualTo(1));
                Assert.That(result.Note, Is.EqualTo("Modelling placeholders substituted: NA_Users by a_testUser, AR_Sales."));
                Assert.That(task.ImplElements.Single(e => e.Field == ElemFieldType.source.ToString()).GroupName, Is.EqualTo(kUserArea), "the original task is unchanged");
            }
        }

        [Test]
        public void RuleModify_UpdatablePlaceholderInDestination_KeepsTheActionOfThePlaceholder()
        {
            WfImplTask task = Task(WfTaskType.rule_modify, Group(kOtherArea, ElemFieldType.source),
                Group(" na_updatable ", ElemFieldType.destination, RequestAction.unchanged), Service());

            ModellingPlaceholderResult result = ModellingPlaceholderSubstitution.Apply(task, Input(Config(kUpdatableType, "Exchange Online China Services")));

            WfImplElement substituted = result.ImplTask.ImplElements.Single(e => e.Field == ElemFieldType.destination.ToString());
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.IsWritable, Is.True);
                Assert.That(NamesOn(result.ImplTask, ElemFieldType.destination), Is.EqualTo(kExchange));
                Assert.That(substituted.ImplAction, Is.EqualTo(RequestAction.unchanged.ToString()));
                Assert.That(substituted.KeepNameVerbatim, Is.True);
            }
        }

        [Test]
        public void Access_BothKindsOnDifferentSides_AreReplaced()
        {
            WfImplTask task = Task(WfTaskType.access, Group(kUserArea, ElemFieldType.modelled_source), Group(kUpdatableArea, ElemFieldType.modelled_destination), Service());

            ModellingPlaceholderResult result = ModellingPlaceholderSubstitution.Apply(task,
                Input(Config(kUserType, "a_testUser"), Config(kUserType, "AR_Sales"), Config(kUpdatableType, "Exchange Online China Services")));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.IsWritable, Is.True);
                Assert.That(NamesOn(result.ImplTask, ElemFieldType.modelled_source), Is.EqualTo(kTwoAccessRoles));
                Assert.That(NamesOn(result.ImplTask, ElemFieldType.modelled_destination), Is.EqualTo(kExchange));
                Assert.That(result.Note, Does.Contain("NA_Users by a_testUser, AR_Sales").And.Contain("NA_Updatable by Exchange Online China Services"));
            }
        }

        [Test]
        public void Access_SeveralPlaceholderAreasOfAKind_AreEachReplacedByAllObjects()
        {
            WfImplTask task = Task(WfTaskType.access, Group(kUserArea, ElemFieldType.source), Group(kSecondUserArea, ElemFieldType.source),
                Group(kOtherArea, ElemFieldType.destination), Service());

            ModellingPlaceholderResult result = ModellingPlaceholderSubstitution.Apply(task, Input(Config(kUserType, "a_testUser"), Config(kUserType, "AR_Sales")));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.IsWritable, Is.True);
                Assert.That(NamesOn(result.ImplTask, ElemFieldType.source), Is.EqualTo(kTwoAccessRoles), "each object once");
            }
        }

        [TestCase(WfTaskType.rule_delete)]
        [TestCase(WfTaskType.group_create)]
        public void OtherTaskTypes_AreNotChanged(WfTaskType taskType)
        {
            WfImplTask task = Task(taskType, Group(kUserArea, ElemFieldType.source));

            ModellingPlaceholderResult result = ModellingPlaceholderSubstitution.Apply(task, Input(Config("NAT", "")));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.IsWritable, Is.True);
                Assert.That(result.ImplTask, Is.SameAs(task));
                Assert.That(result.Note, Is.Null);
            }
        }

        [Test]
        public void Access_WithoutPlaceholdersAndExtraConfigs_IsNotChanged()
        {
            WfImplTask task = Task(WfTaskType.access, Group(kOtherArea, ElemFieldType.source), Group(kOtherArea, ElemFieldType.destination), Service());

            ModellingPlaceholderResult result = ModellingPlaceholderSubstitution.Apply(task, Input(Config("Doku_only", "")));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.IsWritable, Is.True);
                Assert.That(result.ImplTask, Is.SameAs(task));
                Assert.That(result.Note, Is.Null);
            }
        }

        [Test]
        public void Access_ElementWithNameOrAddress_IsNoPlaceholder()
        {
            WfImplElement namedHost = Group(kUserArea, ElemFieldType.source);
            namedHost.Name = "host";
            namedHost.IpString = "10.0.0.1";
            WfImplTask task = Task(WfTaskType.access, namedHost, Group(kOtherArea, ElemFieldType.destination), Service());

            ModellingPlaceholderResult result = ModellingPlaceholderSubstitution.Apply(task, Input());

            Assert.That(result.ImplTask, Is.SameAs(task));
        }

        [TestCase("NAT", "", "'NAT'")]
        [TestCase("Free hint", "use VPN community", "'Free hint: use VPN community'")]
        public void Access_ExtraConfigNotImplementedAutomatically_IsNotWritable(string type, string text, string expectedInReason)
        {
            WfImplTask task = Task(WfTaskType.access, Group(kOtherArea, ElemFieldType.source), Group(kOtherArea, ElemFieldType.destination), Service());

            ModellingPlaceholderResult result = ModellingPlaceholderSubstitution.Apply(task, Input(Config(type, text)));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.IsWritable, Is.False);
                Assert.That(result.NotWritableReason, Does.Contain(expectedInReason).And.Contain("implement it manually"));
            }
        }

        [Test]
        public void Access_ExtraConfigOfTheUsedInterface_IsNotWritable()
        {
            WfImplTask task = Task(WfTaskType.access, Group(kUserArea, ElemFieldType.source), Group(kOtherArea, ElemFieldType.destination), Service());
            List<ModellingExtraConfig> ownConfigs = [Config(kUserType, "a_testUser")];
            List<ModellingExtraConfig> interfaceConfigs = [Config(kUserType, "AR_Interface")];

            ModellingPlaceholderResult result = ModellingPlaceholderSubstitution.Apply(task, new ModellingPlaceholderInput(kAreas, ownConfigs, interfaceConfigs));

            Assert.That(result.NotWritableReason, Does.Contain("interface"));
        }

        [Test]
        public void Access_DocumentationMarkerOfTheUsedInterface_IsIgnored()
        {
            WfImplTask task = Task(WfTaskType.access, Group(kOtherArea, ElemFieldType.source), Group(kOtherArea, ElemFieldType.destination), Service());
            List<ModellingExtraConfig> interfaceConfigs = [Config("Doku_interface", "")];

            ModellingPlaceholderResult result = ModellingPlaceholderSubstitution.Apply(task, new ModellingPlaceholderInput(kAreas, kNoExtraConfigs, interfaceConfigs));

            Assert.That(result.IsWritable, Is.True);
        }

        [Test]
        public void Access_PlaceholdersOfAKindOnBothSides_AreNotWritable()
        {
            WfImplTask task = Task(WfTaskType.access, Group(kUpdatableArea, ElemFieldType.source), Group(kUpdatableArea, ElemFieldType.destination), Service());

            ModellingPlaceholderResult result = ModellingPlaceholderSubstitution.Apply(task, Input(Config(kUpdatableType, "Exchange Online China Services")));

            Assert.That(result.NotWritableReason, Does.Contain("source and destination"));
        }

        [Test]
        public void Access_PlaceholderOnASideNotAllowedBySettings_IsNotWritable()
        {
            WfImplTask task = Task(WfTaskType.access, Group(kOtherArea, ElemFieldType.source), Group(kUserArea, ElemFieldType.destination), Service());

            ModellingPlaceholderResult result = ModellingPlaceholderSubstitution.Apply(task, Input(Config(kUserType, "a_testUser")));

            Assert.That(result.NotWritableReason, Does.Contain("NA_Users is not allowed in the destination"));
        }

        [Test]
        public void Access_PlaceholderWithoutNamedObjects_IsNotWritable()
        {
            WfImplTask task = Task(WfTaskType.access, Group(kUserArea, ElemFieldType.source), Group(kOtherArea, ElemFieldType.destination), Service());

            ModellingPlaceholderResult result = ModellingPlaceholderSubstitution.Apply(task, Input(Config(kUpdatableType, "Exchange Online China Services"), Config(kUserType, " ")));

            Assert.That(result.NotWritableReason, Does.Contain("no extra configuration names the user objects"));
        }

        [Test]
        public void Access_NamedObjectsWithoutPlaceholder_AreNotWritable()
        {
            WfImplTask task = Task(WfTaskType.access, Group(kOtherArea, ElemFieldType.source), Group(kOtherArea, ElemFieldType.destination), Service());

            ModellingPlaceholderResult result = ModellingPlaceholderSubstitution.Apply(task, Input(Config(kUserType, "a_testUser")));

            Assert.That(result.NotWritableReason, Does.Contain("user objects (a_testUser)").And.Contain("no placeholder area"));
        }

        [Test]
        public void Access_FewerObjectsThanPlaceholderAreas_AreNotWritable()
        {
            WfImplTask task = Task(WfTaskType.access, Group(kUserArea, ElemFieldType.source), Group(kSecondUserArea, ElemFieldType.source),
                Group(kOtherArea, ElemFieldType.destination), Service());

            ModellingPlaceholderResult result = ModellingPlaceholderSubstitution.Apply(task, Input(Config(kUserType, "a_testUser")));

            Assert.That(result.NotWritableReason, Does.Contain("1 user objects are named for 2 placeholder areas"));
        }

        [Test]
        public void Access_PlaceholderWithoutKnownConnection_IsNotWritable()
        {
            WfImplTask task = Task(WfTaskType.access, Group(kUserArea, ElemFieldType.source), Group(kOtherArea, ElemFieldType.destination), Service());

            ModellingPlaceholderResult result = ModellingPlaceholderSubstitution.Apply(task, new ModellingPlaceholderInput(kAreas, null, kNoExtraConfigs));

            Assert.That(result.NotWritableReason, Does.Contain("no modelled connection"));
        }

        [Test]
        public void Access_WithoutKnownConnectionAndPlaceholder_IsNotChanged()
        {
            WfImplTask task = Task(WfTaskType.access, Group(kOtherArea, ElemFieldType.source), Group(kOtherArea, ElemFieldType.destination), Service());

            ModellingPlaceholderResult result = ModellingPlaceholderSubstitution.Apply(task, new ModellingPlaceholderInput(kAreas, null, kNoExtraConfigs));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.IsWritable, Is.True);
                Assert.That(result.ImplTask, Is.SameAs(task));
            }
        }

        [Test]
        public void ExtraConfig_RecognizesItsKindByTheNamingConvention()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(Config("IDA-User", "").IsSpecialUser(), Is.True);
                Assert.That(Config("ida_USER", "").IsSpecialUser(), Is.True);
                Assert.That(Config("Users", "").IsSpecialUser(), Is.False);
                Assert.That(Config("Updatable Objects", "").IsUpdatableObject(), Is.True);
                Assert.That(Config("Objects updatable", "").IsUpdatableObject(), Is.False);
                Assert.That(Config("Doku_only", "").IsDocumentationOnly(), Is.True);
                Assert.That(Config("DOoku_only", "").IsDocumentationOnly(), Is.False);
                Assert.That(Config("nat", "").IsNat(), Is.True);
            }
        }
    }
}
