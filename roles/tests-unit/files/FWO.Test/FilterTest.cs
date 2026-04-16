using FWO.Report.Filter;
using FWO.Report.Filter.Exceptions;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Basics;
using FWO.Data.Report;
using FWO.Data.Workflow;
using System.Text.Json;
namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    public class FilterTest
    {
        [SetUp]
        public void Initialize()
        {

        }

        [Test]
        [Parallelizable]
        public void EmptySearch()
        {
            ReportTemplate t = new()
            {
                Filter = ""
            };
            t.ReportParams.ReportType = (int)ReportType.Rules;
            DynGraphqlQuery query = Compiler.Compile(t);

            ClassicAssert.AreEqual(0, query.QueryVariables.Count);
        }

        [Test]
        [Parallelizable]
        public void WhitespaceSearch()
        {
            ReportTemplate t = new()
            {
                Filter = "\t\n  \r  \t \n"
            };
            t.ReportParams.ReportType = (int)ReportType.Rules;
            DynGraphqlQuery query = Compiler.Compile(t);

            ClassicAssert.AreEqual(0, query.QueryVariables.Count);
        }

        [Test]
        [Parallelizable]
        public void TextOnlySearch()
        {
            ReportTemplate t = new()
            {
                Filter = "teststring"
            };
            t.ReportParams.ReportType = (int)ReportType.Rules;
            Compiler.CompileToAst("teststring");
            DynGraphqlQuery query = Compiler.Compile(t);

            ClassicAssert.AreEqual(1, query.QueryVariables.Count);
            ClassicAssert.AreEqual("%teststring%", query.QueryVariables["fullTextFilter0"]);
        }

        [Test]
        [Parallelizable]
        public void AndOr()
        {
            ReportTemplate t = new()
            {
                Filter = "((src=hi) & (dst=test)) | (src = a)"
            };
            t.ReportParams.ReportType = (int)ReportType.Rules;
            DynGraphqlQuery query = Compiler.Compile(t);

            ClassicAssert.AreEqual(3, query.QueryVariables.Count);
            ClassicAssert.AreEqual("%hi%", query.QueryVariables["src0"]);
            ClassicAssert.AreEqual("%test%", query.QueryVariables["dst1"]);
            ClassicAssert.AreEqual("%a%", query.QueryVariables["src2"]);
        }

        [Test]
        [Parallelizable]
        public void TripleOr()
        {
            ReportTemplate t = new()
            {
                Filter = "(src=cactus or dst=cactus or svc=smtps)"
            };
            t.ReportParams.ReportType = (int)ReportType.Rules;
            DynGraphqlQuery query = Compiler.Compile(t);

            ClassicAssert.AreEqual(3, query.QueryVariables.Count);
            ClassicAssert.AreEqual("%cactus%", query.QueryVariables["src0"]);
            ClassicAssert.AreEqual("%cactus%", query.QueryVariables["dst1"]);
            ClassicAssert.AreEqual("%smtps%", query.QueryVariables["svc2"]);
        }

        [Test]
        [Parallelizable]
        public void NotEquals()
        {
            ReportTemplate t = new()
            {
                Filter = "(text!=cactus)"
            };
            t.ReportParams.ReportType = (int)ReportType.Rules;
            DynGraphqlQuery query = Compiler.Compile(t);

            ClassicAssert.AreEqual(1, query.QueryVariables.Count);
            ClassicAssert.AreEqual("cactus", query.QueryVariables["fullTextFilter0"]);
        }

        [Test]
        [Parallelizable]
        public void ExactEquals()
        {
            ReportTemplate t = new()
            {
                Filter = "(text==cactus)"
            };
            t.ReportParams.ReportType = (int)ReportType.Rules;
            DynGraphqlQuery query = Compiler.Compile(t);

            ClassicAssert.AreEqual(1, query.QueryVariables.Count);
            ClassicAssert.AreEqual("cactus", query.QueryVariables["fullTextFilter0"]);
        }

        [Test]
        [Parallelizable]
        public void ExactEquals2()
        {
            ReportTemplate t = new()
            {
                Filter = "(gateway = \"checkpoint_demo\" or gateway = \"fortigate_demo\") & dst == IsoAAADray.local"
            };
            t.ReportParams.ReportType = (int)ReportType.Rules;
            DynGraphqlQuery query = Compiler.Compile(t);

            ClassicAssert.AreEqual(3, query.QueryVariables.Count);
            ClassicAssert.AreEqual("%checkpoint_demo%", query.QueryVariables["gwName0"]);
            ClassicAssert.AreEqual("%fortigate_demo%", query.QueryVariables["gwName1"]);
            ClassicAssert.AreEqual("IsoAAADray.local", query.QueryVariables["dst2"]);
        }

        [Test]
        [Parallelizable]
        public void ExactEquals3()
        {
            try
            {
                ReportTemplate t = new()
                {
                    Filter = "(gateway=\"checkpoint_demo\" or gateway = \"fortigate_demo\") & dst =="
                };
                t.ReportParams.ReportType = (int)ReportType.Rules;
                Compiler.Compile(t);
                Assert.Fail("Exception should have been thrown");
            }
            catch (SyntaxException exception)
            {
                ClassicAssert.AreEqual("No token but one was expected", exception.Message);
            }
        }

        [Test]
        [Parallelizable]
        public void Disabled()
        {
            ReportTemplate t = new()
            {
                Filter = "disabled == true"
            };
            t.ReportParams.ReportType = (int)ReportType.Rules;
            DynGraphqlQuery query = Compiler.Compile(t);

            ClassicAssert.AreEqual(1, query.QueryVariables.Count);
            ClassicAssert.AreEqual("true", query.QueryVariables["disabled0"]);
        }

        [Test]
        [Parallelizable]
        public void Brackets()
        {
            ReportTemplate t = new()
            {
                Filter = "src=a&(dst=c)"
            };
            t.ReportParams.ReportType = (int)ReportType.Rules;
            DynGraphqlQuery query = Compiler.Compile(t);

            ClassicAssert.AreEqual(2, query.QueryVariables.Count);
            ClassicAssert.AreEqual("%a%", query.QueryVariables["src0"]);
            ClassicAssert.AreEqual("%c%", query.QueryVariables["dst1"]);
        }

        [Test]
        [Parallelizable]
        public void RuleRecertPortNot()
        {
            ReportTemplate t = new()
            {
                Filter = "not(port=1000)"
            };
            t.ReportParams.ReportType = (int)ReportType.Recertification;
            DynGraphqlQuery query = Compiler.Compile(t);

            ClassicAssert.AreEqual(3, query.QueryVariables.Count);
            ClassicAssert.AreEqual(true, query.QueryVariables.ContainsKey("refdate1"));
            ClassicAssert.AreEqual(true, query.QueryVariables.ContainsKey("ownerWhere"));
            ClassicAssert.AreEqual("1000", query.QueryVariables["dport0"]);
            ClassicAssert.AreEqual("_and: [{rule_head_text: {_is_null: true}}, { rule_metadatum: { recertifications: { next_recert_date: { _lte: $refdate1 } } } }, {_not: {rule_services: { service: { svcgrp_flats: { serviceBySvcgrpFlatMemberId: { svc_port: {_lte: $dport0}, svc_port_end: {_gte: $dport0 } } } } }}}] ", query.RuleWhereStatement);
        }

        [Test]
        [Parallelizable]
        public void RecertOwnerFilterEmpty()
        {
            ReportTemplate t = new();
            t.ReportParams.ReportType = (int)ReportType.Recertification;
            t.ReportParams.RecertFilter.RecertOwnerList = [];

            DynGraphqlQuery query = Compiler.Compile(t);

            ClassicAssert.AreEqual(true, query.QueryVariables.ContainsKey("ownerWhere"));
            ClassicAssert.AreEqual("{}", JsonSerializer.Serialize(query.QueryVariables["ownerWhere"]));
        }

        [Test]
        [Parallelizable]
        public void RecertQueryBuildsRulebaseRulesBlock()
        {
            ReportTemplate t = new();
            t.ReportParams.ReportType = (int)ReportType.Recertification;

            DynGraphqlQuery query = Compiler.Compile(t);

            StringAssert.Contains("rulebase_links(where:", query.FullQuery);
            StringAssert.DoesNotContain("rulebase_links(where: { }) { rules (", query.FullQuery);
            StringAssert.Contains("rulebases { id uid name rules (", query.FullQuery);
        }

        [Test]
        [Parallelizable]
        public void OwnerFullTextFilterUsesResponsibles()
        {
            ReportTemplate t = new()
            {
                Filter = "text=ops"
            };
            t.ReportParams.ReportType = (int)ReportType.OwnerRecertification;
            DynGraphqlQuery query = Compiler.Compile(t);

            StringAssert.Contains("owner_responsibles", query.OwnerWhereStatement);
            ClassicAssert.IsFalse(query.OwnerWhereStatement.Contains("group_dn"));
        }

        [Test]
        [Parallelizable]
        public void OwnerRecertificationFilterIncludesInactiveOwnersSeparately()
        {
            ReportTemplate t = new();
            t.ReportParams.ReportType = (int)ReportType.OwnerRecertification;
            t.ReportParams.ModellingFilter.SelectedOwners = [new() { Id = 1 }];
            t.ReportParams.ModellingFilter.ShowAllOwners = false;
            t.ReportParams.ModellingFilter.ShowInactiveRecertOwners = true;
            t.ReportParams.RecertFilter.RecertificationDisplayPeriod = 30;

            DynGraphqlQuery query = Compiler.Compile(t);

            StringAssert.Contains("recert_active: { _eq: true }", query.OwnerWhereStatement);
            StringAssert.Contains("next_recert_date: { _lte: $refDate }", query.OwnerWhereStatement);
            StringAssert.Contains("recert_active: { _eq: false }", query.OwnerWhereStatement);
            StringAssert.Contains("_or:", query.OwnerWhereStatement);
        }

        [Test]
        [Parallelizable]
        public void OwnerRecertificationFilterWithZeroDisplayPeriodKeepsOnlyDueActiveOwners()
        {
            ReportTemplate t = new();
            t.ReportParams.ReportType = (int)ReportType.OwnerRecertification;
            t.ReportParams.ModellingFilter.SelectedOwners = [new() { Id = 1 }];
            t.ReportParams.ModellingFilter.ShowAllOwners = false;
            t.ReportParams.ModellingFilter.ShowInactiveRecertOwners = false;
            t.ReportParams.RecertFilter.RecertificationDisplayPeriod = 0;

            DynGraphqlQuery query = Compiler.Compile(t);

            StringAssert.Contains("recert_active: { _eq: true }", query.OwnerWhereStatement);
            StringAssert.Contains("next_recert_date: { _lte: $refDate }", query.OwnerWhereStatement);
            StringAssert.DoesNotContain("recert_active: { _eq: false }", query.OwnerWhereStatement);
            ClassicAssert.IsTrue(query.QueryVariables.ContainsKey("refDate"));
            Assert.That((DateTime)query.QueryVariables["refDate"], Is.EqualTo(DateTime.Now).Within(TimeSpan.FromSeconds(5)));
        }

        [Test]
        [Parallelizable]
        public void ConnIpFilter()
        {
            ReportTemplate t = new()
            {
                Filter = "src=10.0.0.1 or dst=10.0.0.2"
            };
            t.ReportParams.ReportType = (int)ReportType.Connections;
            DynGraphqlQuery query = Compiler.Compile(t);

            ClassicAssert.AreEqual(5, query.QueryVariables.Count);
            ClassicAssert.AreEqual(0, query.QueryVariables["appId"]);
            ClassicAssert.AreEqual("10.0.0.1", query.QueryVariables["srcIpLow0"]);
            ClassicAssert.AreEqual("10.0.0.1", query.QueryVariables["srcIpHigh0"]);
            ClassicAssert.AreEqual("10.0.0.2", query.QueryVariables["dstIpLow1"]);
            ClassicAssert.AreEqual("10.0.0.2", query.QueryVariables["dstIpHigh1"]);
            ClassicAssert.AreEqual("_and: [{ _or: [ { app_id: { _eq: $appId } }, { proposed_app_id: { _eq: $appId } } ], removed: { _eq: false } }{_or: [{ _or: [{ nwobject_connections: {connection_field: { _eq: 1 }, owner_network: {  ip_end: { _gte: $srcIpLow0 } ip: { _lte: $srcIpHigh0 } } } }, { nwgroup_connections: {connection_field: { _eq: 1 }, nwgroup: { nwobject_nwgroups: { owner_network: {  ip_end: { _gte: $srcIpLow0 } ip: { _lte: $srcIpHigh0 } } } } } }]}, { _or: [{ nwobject_connections: {connection_field: { _eq: 2 }, owner_network: {  ip_end: { _gte: $dstIpLow1 } ip: { _lte: $dstIpHigh1 } } } }, { nwgroup_connections: {connection_field: { _eq: 2 }, nwgroup: { nwobject_nwgroups: { owner_network: {  ip_end: { _gte: $dstIpLow1 } ip: { _lte: $dstIpHigh1 } } } } } }]}] }] ", query.ConnectionWhereStatement);
        }

        [Test]
        [Parallelizable]
        public void TicketChangeReport_UsesTicketClosureReferenceDate()
        {
            ReportTemplate template = new()
            {
                Filter = ""
            };
            template.ReportParams.ReportType = (int)ReportType.TicketChangeReport;
            template.ReportParams.TimeFilter.TimeRangeType = TimeRangeType.Shortcut;
            template.ReportParams.TimeFilter.TimeRangeShortcut = "today";
            template.ReportParams.WorkflowFilter.ReferenceDate = WorkflowReferenceDate.TicketClosure;

            DynGraphqlQuery query = Compiler.Compile(template);

            StringAssert.Contains("request_ticket", query.FullQuery);
            StringAssert.Contains("date_completed: { _gte: $ticket_time_start }", query.FullQuery);
            ClassicAssert.IsTrue(query.QueryVariables.ContainsKey("ticket_time_start"));
            ClassicAssert.IsTrue(query.QueryVariables.ContainsKey("ticket_time_end"));
        }

        [Test]
        [Parallelizable]
        public void TicketChangeReport_UsesTicketCreationReferenceDate()
        {
            ReportTemplate template = new()
            {
                Filter = ""
            };
            template.ReportParams.ReportType = (int)ReportType.TicketChangeReport;
            template.ReportParams.TimeFilter.TimeRangeType = TimeRangeType.Shortcut;
            template.ReportParams.TimeFilter.TimeRangeShortcut = "today";
            template.ReportParams.WorkflowFilter.ReferenceDate = WorkflowReferenceDate.TicketCreation;

            DynGraphqlQuery query = Compiler.Compile(template);

            StringAssert.Contains("request_ticket", query.FullQuery);
            StringAssert.Contains("date_created: { _gte: $ticket_time_start }", query.FullQuery);
            StringAssert.DoesNotContain("date_completed: { _is_null: false }", query.FullQuery);
        }

        [Test]
        [Parallelizable]
        public void TicketChangeReport_UsesApprovalOpenedReferenceDate()
        {
            ReportTemplate template = new()
            {
                Filter = ""
            };
            template.ReportParams.ReportType = (int)ReportType.TicketChangeReport;
            template.ReportParams.WorkflowFilter.ReferenceDate = WorkflowReferenceDate.ApprovalOpened;

            DynGraphqlQuery query = Compiler.Compile(template);

            StringAssert.Contains("approvals: { _and: [{ date_opened: { _gte: $ticket_time_start } }", query.FullQuery);
        }

        [Test]
        [Parallelizable]
        public void TicketChangeReport_UsesImplementationStartReferenceDate()
        {
            ReportTemplate template = new()
            {
                Filter = ""
            };
            template.ReportParams.ReportType = (int)ReportType.TicketChangeReport;
            template.ReportParams.WorkflowFilter.ReferenceDate = WorkflowReferenceDate.ImplementationStart;

            DynGraphqlQuery query = Compiler.Compile(template);

            StringAssert.Contains("impltasks: { _and: [{ start: { _gte: $ticket_time_start } }", query.FullQuery);
        }

        [Test]
        [Parallelizable]
        public void TicketChangeReport_UsesAnyActivityReferenceDate()
        {
            ReportTemplate template = new()
            {
                Filter = ""
            };
            template.ReportParams.ReportType = (int)ReportType.TicketChangeReport;
            template.ReportParams.WorkflowFilter.ReferenceDate = WorkflowReferenceDate.AnyActivity;

            DynGraphqlQuery query = Compiler.Compile(template);

            StringAssert.Contains("_or:", query.FullQuery);
            StringAssert.Contains("approval_date", query.FullQuery);
            StringAssert.Contains("date_opened", query.FullQuery);
            StringAssert.Contains("start: { _gte: $ticket_time_start }", query.FullQuery);
            StringAssert.Contains("stop: { _gte: $ticket_time_start }", query.FullQuery);
            StringAssert.Contains("impltasks", query.FullQuery);
        }

        [Test]
        [Parallelizable]
        public void TicketChangeReport_FiltersBySelectedTaskTypes()
        {
            ReportTemplate template = new()
            {
                Filter = ""
            };
            template.ReportParams.ReportType = (int)ReportType.TicketChangeReport;
            template.ReportParams.WorkflowFilter.TaskTypes = [WfTaskType.access, WfTaskType.new_interface];

            DynGraphqlQuery query = Compiler.Compile(template);

            StringAssert.Contains("reqtasks: { task_type: { _in: $task_types } }", query.FullQuery);
            Assert.That(query.QueryVariables["task_types"], Is.EqualTo(new List<string> { WfTaskType.access.ToString(), WfTaskType.new_interface.ToString() }));
        }

        [Test]
        [Parallelizable]
        public void TicketChangeReport_FiltersBySelectedStates()
        {
            ReportTemplate template = new()
            {
                Filter = ""
            };
            template.ReportParams.ReportType = (int)ReportType.TicketChangeReport;
            template.ReportParams.WorkflowFilter.StateIds = [2, 5];

            DynGraphqlQuery query = Compiler.Compile(template);

            StringAssert.Contains("state_id: { _in: $state_ids }", query.FullQuery);
            Assert.That(query.QueryVariables["state_ids"], Is.EqualTo(new List<int> { 2, 5 }));
        }

        [Test]
        [Parallelizable]
        public void TicketReport_UsesTaskTypeStateAndPhaseFiltersWithoutTimeRange()
        {
            ReportTemplate template = new()
            {
                Filter = ""
            };
            template.ReportParams.ReportType = (int)ReportType.TicketReport;
            template.ReportParams.WorkflowFilter.TaskTypes = [WfTaskType.access, WfTaskType.new_interface];
            template.ReportParams.WorkflowFilter.StateIds = [2, 5];
            template.ReportParams.WorkflowFilter.Phase = "implementation";

            DynGraphqlQuery query = Compiler.Compile(template);

            StringAssert.Contains("query ticketReport", query.FullQuery);
            StringAssert.Contains("reqtasks: { task_type: { _in: $task_types } }", query.FullQuery);
            StringAssert.Contains("state_id: { _in: $state_ids }", query.FullQuery);
            StringAssert.Contains("state_id: { _gte: $phase_lowest_input_state, _lt: $phase_lowest_end_state }", query.FullQuery);
            StringAssert.DoesNotContain("$ticket_time_start", query.FullQuery);
            Assert.That(query.QueryVariables["task_types"], Is.EqualTo(new List<string> { WfTaskType.access.ToString(), WfTaskType.new_interface.ToString() }));
            Assert.That(query.QueryVariables["state_ids"], Is.EqualTo(new List<int> { 2, 5 }));
        }

        [Test]
        [Parallelizable]
        public void TicketChangeReport_UsesTaskTypeStatePhaseAndTimeFilters()
        {
            ReportTemplate template = new()
            {
                Filter = ""
            };
            template.ReportParams.ReportType = (int)ReportType.TicketChangeReport;
            template.ReportParams.TimeFilter.TimeRangeType = TimeRangeType.Shortcut;
            template.ReportParams.TimeFilter.TimeRangeShortcut = "today";
            template.ReportParams.WorkflowFilter.TaskTypes = [WfTaskType.access, WfTaskType.new_interface];
            template.ReportParams.WorkflowFilter.StateIds = [2, 5];
            template.ReportParams.WorkflowFilter.Phase = "implementation";

            DynGraphqlQuery query = Compiler.Compile(template);

            StringAssert.Contains("query ticketChangeReport", query.FullQuery);
            StringAssert.Contains("reqtasks: { task_type: { _in: $task_types } }", query.FullQuery);
            StringAssert.Contains("state_id: { _in: $state_ids }", query.FullQuery);
            StringAssert.Contains("state_id: { _gte: $phase_lowest_input_state, _lt: $phase_lowest_end_state }", query.FullQuery);
            StringAssert.Contains("$ticket_time_start", query.FullQuery);
            StringAssert.Contains("$ticket_time_end", query.FullQuery);
            Assert.That(query.QueryVariables["task_types"], Is.EqualTo(new List<string> { WfTaskType.access.ToString(), WfTaskType.new_interface.ToString() }));
            Assert.That(query.QueryVariables["state_ids"], Is.EqualTo(new List<int> { 2, 5 }));
        }

        [Test]
        [Parallelizable]
        public void TicketReport_FilterLineAppliesWorkflowTaskTypeStateAndPhaseFilters()
        {
            ReportTemplate template = new()
            {
                Filter = "tasktype=access,new_interface and states=2,5 and phase=implementation"
            };
            template.ReportParams.ReportType = (int)ReportType.TicketReport;

            DynGraphqlQuery query = Compiler.Compile(template);

            StringAssert.Contains("query ticketReport", query.FullQuery);
            StringAssert.Contains("reqtasks: { task_type: { _in: $task_types } }", query.FullQuery);
            StringAssert.Contains("state_id: { _in: $state_ids }", query.FullQuery);
            StringAssert.Contains("state_id: { _gte: $phase_lowest_input_state, _lt: $phase_lowest_end_state }", query.FullQuery);
            Assert.That(query.QueryVariables["task_types"], Is.EqualTo(new List<string> { WfTaskType.access.ToString(), WfTaskType.new_interface.ToString() }));
            Assert.That(query.QueryVariables["state_ids"], Is.EqualTo(new List<int> { 2, 5 }));
        }

        [Test]
        [Parallelizable]
        public void TicketReport_FilterLineAppliesClosedPhaseFilter()
        {
            ReportTemplate template = new()
            {
                Filter = $"phase={GlobalConst.kClosed}"
            };
            template.ReportParams.ReportType = (int)ReportType.TicketReport;

            DynGraphqlQuery query = Compiler.Compile(template);

            StringAssert.Contains("state_id: { _gte: $phase_lowest_input_state }", query.FullQuery);
            StringAssert.DoesNotContain("$phase_lowest_end_state", query.FullQuery);
        }

        [Test]
        [Parallelizable]
        public void TicketReport_FilterLineRejectsUnknownWorkflowPhase()
        {
            ReportTemplate template = new()
            {
                Filter = "phase=imaginary"
            };
            template.ReportParams.ReportType = (int)ReportType.TicketReport;

            Assert.Throws<SemanticException>(() => Compiler.Compile(template));
        }

        [Test]
        [Parallelizable]
        public void RulesReport_LastHitLessThanIncludesNullHits()
        {
            ReportTemplate template = new()
            {
                Filter = "lasthit<2025-01-01"
            };
            template.ReportParams.ReportType = (int)ReportType.Rules;

            DynGraphqlQuery query = Compiler.Compile(template);

            StringAssert.Contains("rule_last_hit", query.RuleWhereStatement);
            StringAssert.Contains("_is_null: true", query.RuleWhereStatement);
            StringAssert.Contains("_or:", query.RuleWhereStatement);
        }

        [Test]
        [Parallelizable]
        public void RulesReport_LastHitEqualsThisYearUsesStartOfCurrentYear()
        {
            ReportTemplate template = new()
            {
                Filter = "lasthit=\"this year\""
            };
            template.ReportParams.ReportType = (int)ReportType.Rules;

            DynGraphqlQuery query = Compiler.Compile(template);

            string expected = new DateTime(DateTime.Now.Year, 1, 1, 0, 0, 0).ToString(DynGraphqlQuery.fullTimeFormat);
            Assert.That(query.QueryVariables["lastHitLimit0"], Is.EqualTo(expected));
        }

        [Test]
        [Parallelizable]
        public void RulesReport_LastHitRejectsInvalidTimeRangeFormat()
        {
            ReportTemplate template = new()
            {
                Filter = "lasthit=definitely-not-a-date"
            };
            template.ReportParams.ReportType = (int)ReportType.Rules;

            Assert.Throws<SyntaxException>(() => Compiler.Compile(template));
        }

        [Test]
        [Parallelizable]
        public void ChangesReport_LastHitGreaterThanUsesNestedRuleMetadataWithoutNullHits()
        {
            ReportTemplate template = new()
            {
                Filter = "lasthit>2025-01-01"
            };
            template.ReportParams.ReportType = (int)ReportType.Changes;

            DynGraphqlQuery query = Compiler.Compile(template);

            StringAssert.Contains("rule: { rule_metadatum:{ rule_last_hit:", query.RuleWhereStatement);
            StringAssert.DoesNotContain("_is_null: true", query.RuleWhereStatement);
            StringAssert.DoesNotContain("_or:", query.RuleWhereStatement);
        }

        [Test]
        [Parallelizable]
        public void ReportTypeFilter_ParsesStatisticsAliasAndAddsStatisticsRuleFilter()
        {
            ReportTemplate template = new()
            {
                Filter = "type=statistic"
            };
            template.ReportParams.ReportType = (int)ReportType.Rules;

            DynGraphqlQuery query = Compiler.Compile(template);

            Assert.That(query.ReportType, Is.EqualTo(ReportType.Statistics));
            StringAssert.Contains("rule_head_text: {_is_null: true}", query.RuleWhereStatement);
        }

        [Test]
        [Parallelizable]
        public void ReportTypeFilter_RejectsUnknownReportType()
        {
            ReportTemplate template = new()
            {
                Filter = "reporttype=imaginary"
            };
            template.ReportParams.ReportType = (int)ReportType.Rules;

            Assert.Throws<SemanticException>(() => Compiler.Compile(template));
        }

        [Test]
        [Parallelizable]
        public void BoolFilter_RemoveBuildsRuleToBeRemovedFilter()
        {
            ReportTemplate template = new()
            {
                Filter = "remove=true"
            };
            template.ReportParams.ReportType = (int)ReportType.Rules;

            DynGraphqlQuery query = Compiler.Compile(template);

            Assert.That(query.QueryVariables["remove0"], Is.EqualTo("true"));
            StringAssert.Contains("rule_to_be_removed", query.RuleWhereStatement);
        }

        [Test]
        [Parallelizable]
        public void BoolFilter_RejectsInvalidBooleanValue()
        {
            ReportTemplate template = new()
            {
                Filter = "disabled=maybe"
            };
            template.ReportParams.ReportType = (int)ReportType.Rules;

            Assert.Throws<SemanticException>(() => Compiler.Compile(template));
        }

        [Test]
        [Parallelizable]
        public void IntFilter_UnusedBuildsLastHitCutoffFilter()
        {
            ReportTemplate template = new()
            {
                Filter = "unused=30"
            };
            template.ReportParams.ReportType = (int)ReportType.Rules;

            DynGraphqlQuery query = Compiler.Compile(template);

            Assert.That(query.QueryVariables.ContainsKey("cut0"), Is.True);
            StringAssert.Contains("rule_last_hit", query.RuleWhereStatement);
            StringAssert.Contains("_is_null: true", query.RuleWhereStatement);
        }

        [Test]
        [Parallelizable]
        public void IntFilter_RejectsInvalidIntegerValue()
        {
            ReportTemplate template = new()
            {
                Filter = "unused=abc"
            };
            template.ReportParams.ReportType = (int)ReportType.Rules;

            Assert.Throws<SemanticException>(() => Compiler.Compile(template));
        }

        [Test]
        [Parallelizable]
        public void TicketChangeReport_FilterLineAppliesReferenceDateFilter()
        {
            ReportTemplate template = new()
            {
                Filter = "reference_date=ImplementationStart"
            };
            template.ReportParams.ReportType = (int)ReportType.TicketChangeReport;

            DynGraphqlQuery query = Compiler.Compile(template);

            StringAssert.Contains("impltasks: { _and: [{ start: { _gte: $ticket_time_start } }", query.FullQuery);
        }

        [Test]
        [Parallelizable]
        public void TicketReport_FiltersByWorkflowLabelValueTrue()
        {
            ReportTemplate template = new()
            {
                Filter = ""
            };
            template.ReportParams.ReportType = (int)ReportType.TicketReport;
            template.ReportParams.WorkflowFilter.LabelFilter = new() { Name = "policy_check", Mode = WorkflowLabelFilterMode.value, Value = "true" };

            DynGraphqlQuery query = Compiler.Compile(template);

            StringAssert.Contains("reqtasks: { additional_info: { _ilike: $labelValuePattern0 } }", query.FullQuery);
            Assert.That(query.QueryVariables["labelValuePattern0"], Is.EqualTo("%\"policy_check\":\"true\"%"));
        }

        [Test]
        [Parallelizable]
        public void TicketReport_FiltersByWorkflowLabelNotExisting()
        {
            ReportTemplate template = new()
            {
                Filter = ""
            };
            template.ReportParams.ReportType = (int)ReportType.TicketReport;
            template.ReportParams.WorkflowFilter.LabelFilter = new() { Name = "policy_check", Mode = WorkflowLabelFilterMode.not_existing };

            DynGraphqlQuery query = Compiler.Compile(template);

            StringAssert.Contains("_not: { reqtasks: { additional_info: { _ilike: $labelKeyPattern0 } } }", query.FullQuery);
            Assert.That(query.QueryVariables["labelKeyPattern0"], Is.EqualTo("%\"policy_check\":%"));
        }

        [Test]
        [Parallelizable]
        public void TicketReport_FilterLineSupportsSingularStateAlias()
        {
            ReportTemplate template = new()
            {
                Filter = "state=49"
            };
            template.ReportParams.ReportType = (int)ReportType.TicketReport;

            DynGraphqlQuery query = Compiler.Compile(template);

            StringAssert.Contains("query ticketReport", query.FullQuery);
            Assert.That(query.QueryVariables["state_ids"], Is.EqualTo(new List<int> { 49 }));
        }

        [Test]
        [Parallelizable]
        public void TicketReport_FilterLineDoesNotBreakPlainTextStatusSearch()
        {
            ReportTemplate template = new()
            {
                Filter = "status49"
            };
            template.ReportParams.ReportType = (int)ReportType.TicketReport;

            DynGraphqlQuery query = Compiler.Compile(template);

            StringAssert.Contains("query ticketReport", query.FullQuery);
            Assert.That(query.QueryVariables, Does.ContainKey("task_types"));
            Assert.That(query.QueryVariables, Does.Not.ContainKey("state_ids"));
        }
    }
}
