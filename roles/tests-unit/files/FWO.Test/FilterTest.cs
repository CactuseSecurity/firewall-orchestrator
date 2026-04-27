using FWO.Report.Filter;
using FWO.Report.Filter.Ast;
using FWO.Report.Filter.Exceptions;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Basics;
using FWO.Data.Report;
using FWO.Data.Workflow;
using System.Reflection;
using System.Text.Json;
namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    public class FilterTest
    {
        private delegate void StubExtractDelegate(ref DynGraphqlQuery query, ReportType? reportType);

        private sealed class StubAstNode(StubExtractDelegate extractAction) : AstNode
        {
            private readonly StubExtractDelegate _extractAction = extractAction;

            public override void Extract(ref DynGraphqlQuery query, ReportType? reportType)
            {
                _extractAction(ref query, reportType);
            }
        }

        private static object CreateDateTimeRange(TokenKind operatorKind, string value)
        {
            Assembly filterAssembly = typeof(Compiler).Assembly;
            Type astNodeType = filterAssembly.GetType("FWO.Report.Filter.Ast.AstNodeFilterDateTimeRange", throwOnError: true)!;
            object astNode = Activator.CreateInstance(astNodeType, nonPublic: true)!;

            astNodeType.GetProperty("Name")!.SetValue(astNode, new Token(new Range(), "lasthit", TokenKind.LastHit));
            astNodeType.GetProperty("Operator")!.SetValue(astNode, new Token(new Range(), operatorKind.ToString(), operatorKind));
            astNodeType.GetProperty("Value")!.SetValue(astNode, new Token(new Range(), value, TokenKind.Value));

            Type dateTimeRangeType = filterAssembly.GetType("FWO.Report.Filter.FilterTypes.DateTimeRange", throwOnError: true)!;
            return Activator.CreateInstance(dateTimeRangeType, [astNode])!;
        }

        private static DateTime? GetDateTimeRangeBound(object range, string fieldName)
        {
            return (DateTime?)range.GetType().GetField(fieldName)!.GetValue(range);
        }

        private static TException AssertDateTimeRangeThrows<TException>(TokenKind operatorKind, string value)
            where TException : Exception
        {
            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() => CreateDateTimeRange(operatorKind, value))!;
            Assert.That(exception.InnerException, Is.TypeOf<TException>());
            return (TException)exception.InnerException!;
        }

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
        public void DateTimeRange_NowCreatesClosedRangeAtCurrentTime()
        {
            DateTime before = DateTime.Now;

            object range = CreateDateTimeRange(TokenKind.EEQ, "now");

            DateTime after = DateTime.Now;
            DateTime? start = GetDateTimeRangeBound(range, "Start");
            DateTime? end = GetDateTimeRangeBound(range, "End");

            Assert.That(start, Is.Not.Null);
            Assert.That(end, Is.EqualTo(start));
            Assert.That(start, Is.InRange(before, after));
        }

        [Test]
        [Parallelizable]
        public void DateTimeRange_ThisYearCreatesYearBoundaries()
        {
            int currentYear = DateTime.Now.Year;

            object range = CreateDateTimeRange(TokenKind.EEQ, "this year");

            Assert.That(GetDateTimeRangeBound(range, "Start"), Is.EqualTo(new DateTime(currentYear, 1, 1, 0, 0, 0)));
            Assert.That(GetDateTimeRangeBound(range, "End"), Is.EqualTo(new DateTime(currentYear + 1, 1, 1, 0, 0, 0)));
        }

        [Test]
        [Parallelizable]
        public void DateTimeRange_LastYearCreatesYearBoundaries()
        {
            int currentYear = DateTime.Now.Year;

            object range = CreateDateTimeRange(TokenKind.EEQ, "last year");

            Assert.That(GetDateTimeRangeBound(range, "Start"), Is.EqualTo(new DateTime(currentYear - 1, 1, 1, 0, 0, 0)));
            Assert.That(GetDateTimeRangeBound(range, "End"), Is.EqualTo(new DateTime(currentYear, 1, 1, 0, 0, 0)));
        }

        [Test]
        [Parallelizable]
        public void DateTimeRange_LessThanCreatesOpenStartRange()
        {
            DateTime expectedEnd = new(2025, 1, 1, 0, 0, 0);

            object range = CreateDateTimeRange(TokenKind.LSS, "2025-01-01");

            Assert.That(GetDateTimeRangeBound(range, "Start"), Is.Null);
            Assert.That(GetDateTimeRangeBound(range, "End"), Is.EqualTo(expectedEnd));
        }

        [Test]
        [Parallelizable]
        public void DateTimeRange_GreaterThanCreatesOpenEndRange()
        {
            DateTime expectedStart = new(2025, 1, 1, 0, 0, 0);

            object range = CreateDateTimeRange(TokenKind.GRT, "2025-01-01");

            Assert.That(GetDateTimeRangeBound(range, "Start"), Is.EqualTo(expectedStart));
            Assert.That(GetDateTimeRangeBound(range, "End"), Is.Null);
        }

        [Test]
        [Parallelizable]
        public void DateTimeRange_InvalidExactValueRaisesSyntaxException()
        {
            SyntaxException exception = AssertDateTimeRangeThrows<SyntaxException>(TokenKind.EEQ, "not-a-date");

            Assert.That(exception.Message, Does.Contain("Wrong time range format."));
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
        public void OwnersFilterIncludesStateAndCriticality()
        {
            ReportTemplate t = new();
            t.ReportParams.ReportType = (int)ReportType.Owners;
            t.ReportParams.OwnerFilter.SelectedOwnerLifeCycleStateId = 3;
            t.ReportParams.OwnerFilter.SelectedCriticality = "High";

            DynGraphqlQuery query = Compiler.Compile(t);

            StringAssert.Contains("owner_lifecycle_state_id: { _eq: $ownerLifeCycleStateId }", query.OwnerWhereStatement);
            StringAssert.Contains("criticality: { _eq: $ownerCriticality }", query.OwnerWhereStatement);
            Assert.That(query.QueryVariables["ownerLifeCycleStateId"], Is.EqualTo(3));
            Assert.That(query.QueryVariables["ownerCriticality"], Is.EqualTo("High"));
        }

        [Test]
        [Parallelizable]
        public void OwnersFilterLineIncludesStateAndCriticality()
        {
            ReportTemplate t = new()
            {
                Filter = "ownerstate=3 and criticality=High"
            };
            t.ReportParams.ReportType = (int)ReportType.Owners;

            DynGraphqlQuery query = Compiler.Compile(t);

            StringAssert.Contains("owner_lifecycle_state_id: { _eq: $ownerLifeCycleStateId0 }", query.OwnerWhereStatement);
            StringAssert.Contains("criticality: { _ilike: $ownerCriticality1 }", query.OwnerWhereStatement);
            Assert.That(query.QueryVariables["ownerLifeCycleStateId0"], Is.EqualTo(3));
            Assert.That(query.QueryVariables["ownerCriticality1"], Is.EqualTo("%High%"));
        }

        [Test]
        [Parallelizable]
        public void OwnersFilterLineIncludesStateName()
        {
            ReportTemplate t = new()
            {
                Filter = "ownerstate=Production"
            };
            t.ReportParams.ReportType = (int)ReportType.Owners;

            DynGraphqlQuery query = Compiler.Compile(t);

            StringAssert.Contains("owner_lifecycle_state: { name: { _ilike: $ownerLifeCycleStateName0 } }", query.OwnerWhereStatement);
            Assert.That(query.QueryVariables["ownerLifeCycleStateName0"], Is.EqualTo("%Production%"));
        }

        [Test]
        [Parallelizable]
        public void OwnersFilterLineIncludesStateIdNotEquals()
        {
            ReportTemplate t = new()
            {
                Filter = "ownerstate!=3"
            };
            t.ReportParams.ReportType = (int)ReportType.Owners;

            DynGraphqlQuery query = Compiler.Compile(t);

            StringAssert.Contains("owner_lifecycle_state_id: { _neq: $ownerLifeCycleStateId0 }", query.OwnerWhereStatement);
            Assert.That(query.QueryVariables["ownerLifeCycleStateId0"], Is.EqualTo(3));
        }

        [Test]
        [Parallelizable]
        public void OwnersFilterLineIncludesStateIdLessThan()
        {
            ReportTemplate t = new()
            {
                Filter = "ownerstate<3"
            };
            t.ReportParams.ReportType = (int)ReportType.Owners;

            DynGraphqlQuery query = Compiler.Compile(t);

            StringAssert.Contains("owner_lifecycle_state_id: { _lt: $ownerLifeCycleStateId0 }", query.OwnerWhereStatement);
            Assert.That(query.QueryVariables["ownerLifeCycleStateId0"], Is.EqualTo(3));
        }

        [Test]
        [Parallelizable]
        public void OwnersFilterLineIncludesStateIdGreaterThan()
        {
            ReportTemplate t = new()
            {
                Filter = "ownerstate>3"
            };
            t.ReportParams.ReportType = (int)ReportType.Owners;

            DynGraphqlQuery query = Compiler.Compile(t);

            StringAssert.Contains("owner_lifecycle_state_id: { _gt: $ownerLifeCycleStateId0 }", query.OwnerWhereStatement);
            Assert.That(query.QueryVariables["ownerLifeCycleStateId0"], Is.EqualTo(3));
        }

        [Test]
        [Parallelizable]
        public void OwnersFilterLineIncludesStateNameLessThan()
        {
            ReportTemplate t = new()
            {
                Filter = "ownerstate<Production"
            };
            t.ReportParams.ReportType = (int)ReportType.Owners;

            DynGraphqlQuery query = Compiler.Compile(t);

            StringAssert.Contains("owner_lifecycle_state: { name: { _lt: $ownerLifeCycleStateName0 } }", query.OwnerWhereStatement);
            Assert.That(query.QueryVariables["ownerLifeCycleStateName0"], Is.EqualTo("Production"));
        }

        [Test]
        [Parallelizable]
        public void OwnersFilterLineIncludesStateNameNotEquals()
        {
            ReportTemplate t = new()
            {
                Filter = "ownerstate!=Production"
            };
            t.ReportParams.ReportType = (int)ReportType.Owners;

            DynGraphqlQuery query = Compiler.Compile(t);

            StringAssert.Contains("owner_lifecycle_state: { name: { _nilike: $ownerLifeCycleStateName0 } }", query.OwnerWhereStatement);
            Assert.That(query.QueryVariables["ownerLifeCycleStateName0"], Is.EqualTo("Production"));
        }

        [Test]
        [Parallelizable]
        public void OwnersFilterLineIncludesStateNameGreaterThan()
        {
            ReportTemplate t = new()
            {
                Filter = "ownerstate>Production"
            };
            t.ReportParams.ReportType = (int)ReportType.Owners;

            DynGraphqlQuery query = Compiler.Compile(t);

            StringAssert.Contains("owner_lifecycle_state: { name: { _gt: $ownerLifeCycleStateName0 } }", query.OwnerWhereStatement);
            Assert.That(query.QueryVariables["ownerLifeCycleStateName0"], Is.EqualTo("Production"));
        }

        [Test]
        [Parallelizable]
        public void DynGraphqlQuery_OwnersCombinesSidebarAndFilterLineStateFilters()
        {
            ReportTemplate t = new()
            {
                Filter = "ownerstate=Production"
            };
            t.ReportParams.ReportType = (int)ReportType.Owners;
            t.ReportParams.OwnerFilter.SelectedOwnerLifeCycleStateId = 3;

            DynGraphqlQuery query = Compiler.Compile(t);

            StringAssert.Contains("{ id: { _gt: 0 } }", query.OwnerWhereStatement);
            StringAssert.Contains("{ owner_lifecycle_state_id: { _eq: $ownerLifeCycleStateId } }", query.OwnerWhereStatement);
            StringAssert.Contains("owner_lifecycle_state: { name: { _ilike: $ownerLifeCycleStateName0 } }", query.OwnerWhereStatement);
            Assert.That(query.QueryVariables["ownerLifeCycleStateId"], Is.EqualTo(3));
            Assert.That(query.QueryVariables["ownerLifeCycleStateName0"], Is.EqualTo("%Production%"));
        }

        [Test]
        [Parallelizable]
        public void DynGraphqlQuery_OwnersCombinesSidebarAndFilterLineCriticalityFilters()
        {
            ReportTemplate t = new()
            {
                Filter = "criticality=High"
            };
            t.ReportParams.ReportType = (int)ReportType.Owners;
            t.ReportParams.OwnerFilter.SelectedCriticality = "Medium";

            DynGraphqlQuery query = Compiler.Compile(t);

            StringAssert.Contains("{ id: { _gt: 0 } }", query.OwnerWhereStatement);
            StringAssert.Contains("{ criticality: { _eq: $ownerCriticality } }", query.OwnerWhereStatement);
            StringAssert.Contains("criticality: { _ilike: $ownerCriticality0 }", query.OwnerWhereStatement);
            Assert.That(query.QueryVariables["ownerCriticality"], Is.EqualTo("Medium"));
            Assert.That(query.QueryVariables["ownerCriticality0"], Is.EqualTo("%High%"));
        }

        [Test]
        [Parallelizable]
        public void DynGraphqlQuery_OwnersBuildsOwnerQueryWithNameOrdering()
        {
            ReportTemplate t = new()
            {
                Filter = "criticality=High"
            };
            t.ReportParams.ReportType = (int)ReportType.Owners;

            DynGraphqlQuery query = Compiler.Compile(t);

            StringAssert.Contains("query getOwners", query.FullQuery);
            StringAssert.Contains("owner (where:", query.FullQuery);
            StringAssert.Contains("order_by: { name: asc }", query.FullQuery);
            StringAssert.DoesNotContain("$mgmId: [Int!]", query.FullQuery);
            Assert.That(query.QueryVariables["ownerCriticality0"], Is.EqualTo("%High%"));
        }

        [Test]
        [Parallelizable]
        public void DynGraphqlQuery_OwnerRecertificationBuildsOwnerQueryWithRecertOrdering()
        {
            ReportTemplate t = new();
            t.ReportParams.ReportType = (int)ReportType.OwnerRecertification;
            t.ReportParams.ModellingFilter.SelectedOwners = [new() { Id = 1 }];

            DynGraphqlQuery query = Compiler.Compile(t);

            StringAssert.Contains("query getOwners", query.FullQuery);
            StringAssert.Contains("owner (where:", query.FullQuery);
            StringAssert.Contains("order_by: { next_recert_date: desc, name: asc }", query.FullQuery);
            Assert.That(query.QueryVariables["selectedOwners"], Is.EqualTo(new[] { 1 }));
            Assert.That(query.QueryVariables.ContainsKey("refDate"), Is.True);
        }

        [Test]
        [Parallelizable]
        public void ManagementFilterUsesNumericIdAcrossAllObjectQueries()
        {
            ReportTemplate t = new()
            {
                Filter = "management=7"
            };
            t.ReportParams.ReportType = (int)ReportType.Rules;

            DynGraphqlQuery query = Compiler.Compile(t);

            StringAssert.Contains("management: {mgm_id : {_ilike: $mgmId0 } }", query.RuleWhereStatement);
            StringAssert.Contains("management: {mgm_id : {_ilike: $mgmId0 } }", query.NwObjWhereStatement);
            StringAssert.Contains("management: {mgm_id : {_ilike: $mgmId0 } }", query.SvcObjWhereStatement);
            StringAssert.Contains("management: {mgm_id : {_ilike: $mgmId0 } }", query.UserObjWhereStatement);
            Assert.That(query.QueryVariables["mgmId0"], Is.EqualTo("%7%"));
        }

        [Test]
        [Parallelizable]
        public void ManagementFilterUsesNameAcrossAllObjectQueries()
        {
            ReportTemplate t = new()
            {
                Filter = "management=demo"
            };
            t.ReportParams.ReportType = (int)ReportType.Rules;

            DynGraphqlQuery query = Compiler.Compile(t);

            StringAssert.Contains("management: {mgm_name : {_ilike: $mgmName0 } }", query.RuleWhereStatement);
            StringAssert.Contains("management: {mgm_name : {_ilike: $mgmName0 } }", query.NwObjWhereStatement);
            StringAssert.Contains("management: {mgm_name : {_ilike: $mgmName0 } }", query.SvcObjWhereStatement);
            StringAssert.Contains("management: {mgm_name : {_ilike: $mgmName0 } }", query.UserObjWhereStatement);
            Assert.That(query.QueryVariables["mgmName0"], Is.EqualTo("%demo%"));
        }

        [Test]
        [Parallelizable]
        public void ProtocolFilterTargetsRuleAndConnectionQueries()
        {
            ReportTemplate t = new()
            {
                Filter = "protocol=tcp"
            };
            t.ReportParams.ReportType = (int)ReportType.Rules;

            DynGraphqlQuery query = Compiler.Compile(t);

            StringAssert.Contains("ip_proto_name: { _ilike: $proto0 }", query.RuleWhereStatement);
            StringAssert.Contains("ip_proto_name: { _ilike: $proto0 }", query.ConnectionWhereStatement);
            Assert.That(query.QueryVariables["proto0"], Is.EqualTo("%tcp%"));
        }

        [Test]
        [Parallelizable]
        public void ActionFilterTargetsRuleQuery()
        {
            ReportTemplate t = new()
            {
                Filter = "action=accept"
            };
            t.ReportParams.ReportType = (int)ReportType.Rules;

            DynGraphqlQuery query = Compiler.Compile(t);

            StringAssert.Contains("rule_action: { _ilike: $action0 }", query.RuleWhereStatement);
            Assert.That(query.QueryVariables["action0"], Is.EqualTo("%accept%"));
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
        public void ChangesReport_LastHitLessThanUsesNestedRuleMetadataAndIncludesNullHits()
        {
            ReportTemplate template = new()
            {
                Filter = "lasthit<2025-01-01"
            };
            template.ReportParams.ReportType = (int)ReportType.Changes;

            DynGraphqlQuery query = Compiler.Compile(template);

            StringAssert.Contains("rule: { rule_metadatum: { rule_last_hit:", query.RuleWhereStatement);
            StringAssert.Contains("_is_null: true", query.RuleWhereStatement);
            StringAssert.Contains("_or:", query.RuleWhereStatement);
        }

        [Test]
        [Parallelizable]
        public void RulesReport_LastHitGreaterThanOmitsNullHits()
        {
            ReportTemplate template = new()
            {
                Filter = "lasthit>2025-01-01"
            };
            template.ReportParams.ReportType = (int)ReportType.Rules;

            DynGraphqlQuery query = Compiler.Compile(template);

            StringAssert.Contains("rule_metadatum: { rule_last_hit:", query.RuleWhereStatement);
            StringAssert.DoesNotContain("rule_last_hit: {_is_null: true", query.RuleWhereStatement);
            StringAssert.DoesNotContain("rule: { rule_metadatum", query.RuleWhereStatement);
        }

        [Test]
        [Parallelizable]
        public void LastHitNotEqualsRaisesSemanticException()
        {
            ReportTemplate template = new()
            {
                Filter = "lasthit!=2025-01-01"
            };
            template.ReportParams.ReportType = (int)ReportType.Rules;

            Assert.Throws<SemanticException>(() => Compiler.Compile(template));
        }

        [Test]
        [Parallelizable]
        public void OrConnectorBuildsOrWhereClause()
        {
            ReportTemplate template = new()
            {
                Filter = "action=accept or protocol=tcp"
            };
            template.ReportParams.ReportType = (int)ReportType.Rules;

            DynGraphqlQuery query = Compiler.Compile(template);

            StringAssert.Contains("{_or: [{rule_action: { _ilike: $action0 }}, {rule_services: {service: {stm_ip_proto: {ip_proto_name: { _ilike: $proto1 } } } }}] }", query.RuleWhereStatement);
            StringAssert.Contains("rule_action: { _ilike: $action0 }", query.RuleWhereStatement);
            StringAssert.Contains("ip_proto_name: { _ilike: $proto1 }", query.RuleWhereStatement);
        }

        [Test]
        [Parallelizable]
        public void AstNodeConnector_AndExtractWrapsAllWhereStatements()
        {
            DynGraphqlQuery query = new("");
            AstNodeConnector connector = new()
            {
                Connector = new Token(new Range(), "and", TokenKind.And),
                Left = new StubAstNode((ref DynGraphqlQuery q, ReportType? _) =>
                {
                    q.RuleWhereStatement += "left-rule";
                    q.NwObjWhereStatement += "left-nw";
                    q.SvcObjWhereStatement += "left-svc";
                    q.UserObjWhereStatement += "left-user";
                    q.ConnectionWhereStatement += "left-conn";
                }),
                Right = new StubAstNode((ref DynGraphqlQuery q, ReportType? _) =>
                {
                    q.RuleWhereStatement += "right-rule";
                    q.NwObjWhereStatement += "right-nw";
                    q.SvcObjWhereStatement += "right-svc";
                    q.UserObjWhereStatement += "right-user";
                    q.ConnectionWhereStatement += "right-conn";
                })
            };

            connector.Extract(ref query, ReportType.Rules);

            Assert.That(query.RuleWhereStatement, Is.EqualTo("_and: [{left-rule}, {right-rule}] "));
            Assert.That(query.NwObjWhereStatement, Is.EqualTo("_and: [{left-nw}, {right-nw}] "));
            Assert.That(query.SvcObjWhereStatement, Is.EqualTo("_and: [{left-svc}, {right-svc}] "));
            Assert.That(query.UserObjWhereStatement, Is.EqualTo("_and: [{left-user}, {right-user}] "));
            Assert.That(query.ConnectionWhereStatement, Is.EqualTo("_and: [{left-conn}, {right-conn}] "));
        }

        [Test]
        [Parallelizable]
        public void AstNodeConnector_ExtractThrowsWhenConnectorMissing()
        {
            DynGraphqlQuery query = new("");
            AstNodeConnector connector = new()
            {
                Left = new StubAstNode((ref DynGraphqlQuery _, ReportType? _) => { }),
                Right = new StubAstNode((ref DynGraphqlQuery _, ReportType? _) => { })
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => connector.Extract(ref query, ReportType.Rules))!;

            Assert.That(exception.Message, Does.Contain("Connector"));
        }

        [Test]
        [Parallelizable]
        public void AstNodeConnector_ExtractThrowsWhenLeftMissing()
        {
            DynGraphqlQuery query = new("");
            AstNodeConnector connector = new()
            {
                Connector = new Token(new Range(), "and", TokenKind.And),
                Right = new StubAstNode((ref DynGraphqlQuery _, ReportType? _) => { })
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => connector.Extract(ref query, ReportType.Rules))!;

            Assert.That(exception.Message, Does.Contain("Left"));
        }

        [Test]
        [Parallelizable]
        public void AstNodeConnector_ExtractThrowsWhenRightMissing()
        {
            DynGraphqlQuery query = new("");
            AstNodeConnector connector = new()
            {
                Connector = new Token(new Range(), "and", TokenKind.And),
                Left = new StubAstNode((ref DynGraphqlQuery _, ReportType? _) => { })
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => connector.Extract(ref query, ReportType.Rules))!;

            Assert.That(exception.Message, Does.Contain("Right"));
        }

        [Test]
        [Parallelizable]
        public void AstNodeConnector_ExtractThrowsForUnsupportedConnector()
        {
            DynGraphqlQuery query = new("");
            AstNodeConnector connector = new()
            {
                Connector = new Token(new Range(), "xor", TokenKind.Not),
                Left = new StubAstNode((ref DynGraphqlQuery _, ReportType? _) => { }),
                Right = new StubAstNode((ref DynGraphqlQuery _, ReportType? _) => { })
            };

            SemanticException exception = Assert.Throws<SemanticException>(() => connector.Extract(ref query, ReportType.Rules))!;

            Assert.That(exception.Message, Does.Contain("unsupported connector token"));
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
        public void BoolFilter_DisabledNotEqualsBuildsNegativeRuleDisabledFilter()
        {
            ReportTemplate template = new()
            {
                Filter = "disabled!=true"
            };
            template.ReportParams.ReportType = (int)ReportType.Rules;

            DynGraphqlQuery query = Compiler.Compile(template);

            Assert.That(query.QueryVariables["disabled0"], Is.EqualTo("true"));
            StringAssert.Contains("rule_disabled: { _neq: $disabled0 }", query.RuleWhereStatement);
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
        public void IntFilter_DestinationPortBuildsRuleAndConnectionFilters()
        {
            ReportTemplate template = new()
            {
                Filter = "port=443"
            };
            template.ReportParams.ReportType = (int)ReportType.Rules;

            DynGraphqlQuery query = Compiler.Compile(template);

            Assert.That(query.QueryVariables["dport0"], Is.EqualTo("443"));
            StringAssert.Contains("svc_port: {_lte: $dport0}", query.RuleWhereStatement);
            StringAssert.Contains("service_connections: {service: { port: { _lte: $dport0 }, port_end: { _gte: $dport0 } } }", query.ConnectionWhereStatement);
            StringAssert.Contains("service_group_connections: {service_group: { service_service_groups:", query.ConnectionWhereStatement);
        }

        [Test]
        [Parallelizable]
        public void NetworkFilter_SourceNameExactEqualsBuildsNameFilters()
        {
            ReportTemplate template = new()
            {
                Filter = "src==AppServer1"
            };
            template.ReportParams.ReportType = (int)ReportType.Rules;

            DynGraphqlQuery query = Compiler.Compile(template);

            Assert.That(query.QueryVariables["src0"], Is.EqualTo("AppServer1"));
            StringAssert.Contains("rule_froms: { object: { objgrp_flats: { objectByObjgrpFlatMemberId: { obj_name: { _eq: $src0 } } } } }", query.RuleWhereStatement);
            StringAssert.Contains("owner_network: {name: { _eq: $src0 } }", query.ConnectionWhereStatement);
            StringAssert.Contains("id_string: { _eq: $src0 }", query.ConnectionWhereStatement);
        }

        [Test]
        [Parallelizable]
        public void NetworkFilter_DestinationNameNotEqualsBuildsNegativeNameFilters()
        {
            ReportTemplate template = new()
            {
                Filter = "dst!=AppRole1"
            };
            template.ReportParams.ReportType = (int)ReportType.Rules;

            DynGraphqlQuery query = Compiler.Compile(template);

            Assert.That(query.QueryVariables["dst0"], Is.EqualTo("AppRole1"));
            StringAssert.Contains("obj_name: { _nilike: $dst0 }", query.RuleWhereStatement);
            StringAssert.Contains("owner_network: {name: { _nilike: $dst0 } }", query.ConnectionWhereStatement);
            StringAssert.Contains("id_string: { _nilike: $dst0 }", query.ConnectionWhereStatement);
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
