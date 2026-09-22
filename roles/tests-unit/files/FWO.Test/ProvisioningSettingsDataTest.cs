using FWO.Data.Provisioning;
using FWO.Ui.Pages.Settings;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Covers the binding between the provisioning settings UI and the FWO.Data.Provisioning
    /// settings classes: reading, writing and clearing fields, and resolving a value along the
    /// Global / DeviceType / Management / Gateway chain.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class ProvisioningSettingsDataTest
    {
        private static ProvisioningFieldDefinition Field(string key) =>
            ProvisioningSettingsData.FindField(key) ?? throw new AssertionException($"unknown field {key}");

        private static ProvisioningNode Node(List<ProvisioningNode> nodes, string id) =>
            ProvisioningSettingsData.NodeById(nodes, id) ?? throw new AssertionException($"unknown node {id}");

        [Test]
        public void SampleHierarchy_LinksEveryScopeToItsParent()
        {
            List<ProvisioningNode> nodes = ProvisioningSettingsData.BuildSampleHierarchy();
            ProvisioningNode global = Node(nodes, ProvisioningSettingsData.GlobalNodeKey);

            Assert.Multiple(() =>
            {
                Assert.That(global.Level, Is.EqualTo(ProvisioningScopeType.Global));
                Assert.That(global.ParentNodeId, Is.Null);
                Assert.That(Node(nodes, "dt-fortimanager").Level, Is.EqualTo(ProvisioningScopeType.DeviceType));
                Assert.That(Node(nodes, "mgr-forti-mgr2").Level, Is.EqualTo(ProvisioningScopeType.Management));
                Assert.That(Node(nodes, "gw-forti-gw-2").Level, Is.EqualTo(ProvisioningScopeType.Gateway));
                Assert.That(nodes.Where(n => n.NodeId != global.NodeId).All(n => n.ParentNodeId != null), Is.True);
                Assert.That(nodes.Select(n => n.NodeId).Distinct().Count(), Is.EqualTo(nodes.Count));
            });
        }

        [Test]
        public void SampleHierarchy_ScopesBelowGlobalStartOutInheritingEverything()
        {
            List<ProvisioningNode> nodes = ProvisioningSettingsData.BuildSampleHierarchy();
            ProvisioningNode checkPoint = Node(nodes, "dt-checkpoint");

            Assert.That(ProvisioningSettingsData.Fields.Where(f => f.IsSetOn(checkPoint.Settings)), Is.Empty);
        }

        [Test]
        public void SampleHierarchy_GlobalSetsEveryFieldItOwns()
        {
            List<ProvisioningNode> nodes = ProvisioningSettingsData.BuildSampleHierarchy();
            ProvisioningNode global = Node(nodes, ProvisioningSettingsData.GlobalNodeKey);

            IEnumerable<ProvisioningFieldDefinition> globalFields = ProvisioningSettingsData.Fields
                .Where(f => f.AppliesToLevel(ProvisioningScopeType.Global) && f.Key != ProvisioningSettingsData.FieldTemplates);

            Assert.That(globalFields.Where(f => !f.IsSetOn(global.Settings)), Is.Empty);
        }

        [Test]
        public void Resolve_FallsBackToTheNearestAncestorThatSetsTheField()
        {
            List<ProvisioningNode> nodes = ProvisioningSettingsData.BuildSampleHierarchy();
            ProvisioningFieldDefinition logging = Field(ProvisioningSettingsData.FieldLogging);

            // gw-cp-gw-1 sets Logging itself, its sibling gw-cp-gw-2 inherits it from Global.
            ProvisioningResolvedValue setHere = ProvisioningSettingsData.Resolve(nodes, Node(nodes, "gw-cp-gw-1"), logging);
            ProvisioningResolvedValue inherited = ProvisioningSettingsData.Resolve(nodes, Node(nodes, "gw-cp-gw-2"), logging);

            Assert.Multiple(() =>
            {
                Assert.That(setHere.IsOverriddenHere, Is.True);
                Assert.That(setHere.Value, Is.EqualTo(nameof(ProvisioningLoggingMode.LogTrack)));
                Assert.That(setHere.SourceNode?.Id, Is.EqualTo("gw-cp-gw-1"));

                Assert.That(inherited.IsOverriddenHere, Is.False);
                Assert.That(inherited.Value, Is.EqualTo(nameof(ProvisioningLoggingMode.Log)));
                Assert.That(inherited.SourceNode?.Id, Is.EqualTo(ProvisioningSettingsData.GlobalNodeKey));
            });
        }

        [Test]
        public void Resolve_PrefersTheManagementValueOverTheDeviceTypeValue()
        {
            List<ProvisioningNode> nodes = ProvisioningSettingsData.BuildSampleHierarchy();
            ProvisioningFieldDefinition profiles = Field(ProvisioningSettingsData.FieldSecurityProfiles);

            ProvisioningResolvedValue fromManagement = ProvisioningSettingsData.Resolve(nodes, Node(nodes, "gw-forti-gw-2"), profiles);
            ProvisioningResolvedValue fromDeviceType = ProvisioningSettingsData.Resolve(nodes, Node(nodes, "gw-forti-gw-1"), profiles);

            Assert.Multiple(() =>
            {
                Assert.That(fromManagement.SourceNode?.Id, Is.EqualTo("mgr-forti-mgr2"));
                Assert.That(fromManagement.Value, Is.EqualTo("default,strict-web,ips-high"));
                Assert.That(fromDeviceType.SourceNode?.Id, Is.EqualTo("dt-fortimanager"));
                Assert.That(fromDeviceType.Value, Is.EqualTo("default,strict-web"));
            });
        }

        [Test]
        public void Resolve_FallsBackToTheSchemaDefaultWhenNoScopeSetsTheField()
        {
            List<ProvisioningNode> nodes = ProvisioningSettingsData.BuildSampleHierarchy();
            ProvisioningFieldDefinition ruleCategory = Field(ProvisioningSettingsData.FieldRuleCategory);

            ProvisioningResolvedValue resolved = ProvisioningSettingsData.Resolve(nodes, Node(nodes, "dt-checkpoint"), ruleCategory);

            Assert.Multiple(() =>
            {
                Assert.That(resolved.SourceNode, Is.Null);
                Assert.That(resolved.IsOverriddenHere, Is.False);
                Assert.That(resolved.Value, Is.EqualTo(ruleCategory.DefaultValue));
            });
        }

        [Test]
        public void WriteThenClear_SetsAndResetsTheUnderlyingDataLayerProperty()
        {
            List<ProvisioningNode> nodes = ProvisioningSettingsData.BuildSampleHierarchy();
            ProvisioningNode gateway = Node(nodes, "gw-cp-gw-3");
            ProvisioningFieldDefinition implementationMode = Field(ProvisioningSettingsData.FieldImplementationMode);

            implementationMode.Write(gateway.Settings, nameof(ProvisioningImplementationMode.TufinSc));

            Assert.Multiple(() =>
            {
                Assert.That(gateway.Settings.ImplementationMode, Is.EqualTo(ProvisioningImplementationMode.TufinSc));
                Assert.That(implementationMode.IsSetOn(gateway.Settings), Is.True);
            });

            implementationMode.Clear(gateway.Settings);

            Assert.Multiple(() =>
            {
                Assert.That(gateway.Settings.ImplementationMode, Is.EqualTo(ProvisioningImplementationMode.Undefined));
                Assert.That(implementationMode.IsSetOn(gateway.Settings), Is.False);
                Assert.That(ProvisioningSettingsData.Resolve(nodes, gateway, implementationMode).SourceNode?.Id,
                    Is.EqualTo(ProvisioningSettingsData.GlobalNodeKey));
            });
        }

        [Test]
        public void Write_OfDeviceTypeOnlyFieldsReachesTheSubclassProperties()
        {
            List<ProvisioningNode> nodes = ProvisioningSettingsData.BuildSampleHierarchy();
            ProvisioningNode gateway = Node(nodes, "gw-forti-gw-3");

            Field(ProvisioningSettingsData.FieldPositioningAlgorithm)
                .Write(gateway.Settings, nameof(ProvisioningPositioningAlgorithm.FortinetEndOfZone));
            Field(ProvisioningSettingsData.FieldSecurityProfiles).Write(gateway.Settings, "a,b");
            Field(ProvisioningSettingsData.FieldZoneFrom).Write(gateway.Settings, "dmz");

            DeviceTypeProvisioningSettings settings = (DeviceTypeProvisioningSettings)gateway.Settings;

            Assert.Multiple(() =>
            {
                Assert.That(settings.PositioningAlgorithm, Is.EqualTo(ProvisioningPositioningAlgorithm.FortinetEndOfZone));
                Assert.That(settings.SecurityProfiles, Is.EqualTo(new List<string> { "a", "b" }));
                Assert.That(settings.ZoneFrom, Is.EqualTo("dmz"));
            });
        }

        [Test]
        public void DeviceTypeOnlyFields_AreInertOnAGlobalSettingsObject()
        {
            GlobalProvisioningSettings global = new();
            ProvisioningFieldDefinition zoneTo = Field(ProvisioningSettingsData.FieldZoneTo);

            zoneTo.Write(global, "untrust");
            zoneTo.Clear(global);

            Assert.That(zoneTo.Read(global), Is.Empty);
        }

        [Test]
        public void Write_OfAnUnknownEnumMemberClearsTheField()
        {
            GlobalProvisioningSettings settings = new();
            ProvisioningFieldDefinition ruleType = Field(ProvisioningSettingsData.FieldRuleType);

            ruleType.Write(settings, "not-a-member");

            Assert.Multiple(() =>
            {
                Assert.That(settings.RuleType, Is.EqualTo(ProvisioningRuleType.Undefined));
                Assert.That(ruleType.IsSetOn(settings), Is.False);
            });
        }

        [Test]
        public void SelectOptions_UseTheDataLayerEnumMemberNames()
        {
            foreach (ProvisioningFieldDefinition field in ProvisioningSettingsData.Fields
                .Where(f => f.Kind == ProvisioningFieldKind.SingleSelect))
            {
                GlobalProvisioningSettings settings = field.MinLevel >= ProvisioningScopeType.DeviceType
                    ? new GatewayProvisioningSettings()
                    : new GlobalProvisioningSettings();

                Assert.That(field.Options, Is.Not.Empty, field.Key);
                Assert.That(field.Options.Select(o => o.Value), Has.Member(field.DefaultValue), field.Key);

                foreach (ProvisioningFieldOption option in field.Options)
                {
                    field.Write(settings, option.Value);
                    Assert.That(field.Read(settings), Is.EqualTo(option.Value), $"{field.Key}/{option.Value}");
                }
            }
        }

        [Test]
        public void GetChildren_ReturnsTheDirectChildrenOfAScope()
        {
            List<ProvisioningNode> nodes = ProvisioningSettingsData.BuildSampleHierarchy();

            Assert.Multiple(() =>
            {
                Assert.That(ProvisioningSettingsData.GetChildren(nodes, ProvisioningSettingsData.GlobalNodeKey).Select(n => n.Id),
                    Is.EquivalentTo(new[] { "dt-checkpoint", "dt-fortimanager", "dt-tufinsc" }));
                Assert.That(ProvisioningSettingsData.GetChildren(nodes, "mgr-forti-mgr2").Select(n => n.Id),
                    Is.EquivalentTo(new[] { "gw-forti-gw-2", "gw-forti-gw-3" }));
                Assert.That(ProvisioningSettingsData.GetChildren(nodes, "gw-forti-gw-2"), Is.Empty);
                Assert.That(ProvisioningSettingsData.GetChildren(nodes, "does-not-exist"), Is.Empty);
                Assert.That(ProvisioningSettingsData.GetChildren(nodes, null), Is.Empty);
            });
        }

        [Test]
        public void GetAncestryChain_RunsFromTheScopeUpToGlobal()
        {
            List<ProvisioningNode> nodes = ProvisioningSettingsData.BuildSampleHierarchy();

            List<ProvisioningNode> chain = ProvisioningSettingsData.GetAncestryChain(nodes, Node(nodes, "gw-forti-gw-2"));

            Assert.That(chain.Select(n => n.Id),
                Is.EqualTo(new[] { "gw-forti-gw-2", "mgr-forti-mgr2", "dt-fortimanager", ProvisioningSettingsData.GlobalNodeKey }));
        }

        [Test]
        public void AppliesToLevel_HonoursTheConfiguredScopeRange()
        {
            ProvisioningFieldDefinition pathAnalysis = Field(ProvisioningSettingsData.FieldPathAnalysisAlgorithm);
            ProvisioningFieldDefinition objectCreation = Field(ProvisioningSettingsData.FieldServiceObjectCreation);
            ProvisioningFieldDefinition positioning = Field(ProvisioningSettingsData.FieldPositioningAlgorithm);

            Assert.Multiple(() =>
            {
                Assert.That(pathAnalysis.AppliesToLevel(ProvisioningScopeType.Global), Is.True);
                Assert.That(pathAnalysis.AppliesToLevel(ProvisioningScopeType.DeviceType), Is.False);
                Assert.That(objectCreation.AppliesToLevel(ProvisioningScopeType.Management), Is.True);
                Assert.That(objectCreation.AppliesToLevel(ProvisioningScopeType.Gateway), Is.False);
                Assert.That(positioning.AppliesToLevel(ProvisioningScopeType.Global), Is.False);
                Assert.That(positioning.AppliesToLevel(ProvisioningScopeType.Gateway), Is.True);
            });
        }

        [Test]
        public void ParseStringList_KeepsEmptyEntriesButNotForAnEmptyValue()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ProvisioningSettingsData.ParseStringList(""), Is.Empty);
                Assert.That(ProvisioningSettingsData.ParseStringList("a, b"), Is.EqualTo(new[] { "a", "b" }));
                Assert.That(ProvisioningSettingsData.ParseStringList("a,"), Is.EqualTo(new[] { "a", "" }));
            });
        }

        [Test]
        public void FindField_ReturnsNullForAnUnknownKey()
        {
            Assert.That(ProvisioningSettingsData.FindField("no-such-field"), Is.Null);
        }
    }
}
