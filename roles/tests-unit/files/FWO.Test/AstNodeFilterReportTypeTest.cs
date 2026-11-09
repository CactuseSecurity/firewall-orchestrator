using FWO.Basics;
using FWO.Data.Report;
using FWO.Report.Filter;
using FWO.Report.Filter.Exceptions;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class AstNodeFilterReportTypeTest
    {
        [TestCase("rules", ReportType.Rules)]
        [TestCase("rule", ReportType.Rules)]
        [TestCase("resolvedrules", ReportType.ResolvedRules)]
        [TestCase("resolvedrule", ReportType.ResolvedRules)]
        [TestCase("resolvedrulestech", ReportType.ResolvedRulesTech)]
        [TestCase("resolvedruletech", ReportType.ResolvedRulesTech)]
        [TestCase("unusedrules", ReportType.UnusedRules)]
        [TestCase("unusedrule", ReportType.UnusedRules)]
        [TestCase("statistics", ReportType.Statistics)]
        [TestCase("statistic", ReportType.Statistics)]
        [TestCase("changes", ReportType.Changes)]
        [TestCase("change", ReportType.Changes)]
        [TestCase("resolvedchanges", ReportType.ResolvedChanges)]
        [TestCase("resolvedchange", ReportType.ResolvedChanges)]
        [TestCase("resolvedchangestech", ReportType.ResolvedChangesTech)]
        [TestCase("resolvedchangetech", ReportType.ResolvedChangesTech)]
        [TestCase("natrules", ReportType.NatRules)]
        [TestCase("nat_rules", ReportType.NatRules)]
        [TestCase("natchanges", ReportType.NatChanges)]
        [TestCase("nat_changes", ReportType.NatChanges)]
        [TestCase("recertifications", ReportType.Recertification)]
        [TestCase("recertification", ReportType.Recertification)]
        [TestCase("connections", ReportType.Connections)]
        [TestCase("connection", ReportType.Connections)]
        [TestCase("apprules", ReportType.AppRules)]
        [TestCase("apprule", ReportType.AppRules)]
        [TestCase("variances", ReportType.VarianceAnalysis)]
        [TestCase("variance", ReportType.VarianceAnalysis)]
        [TestCase("ownerrecertifications", ReportType.OwnerRecertification)]
        [TestCase("ownerrecertification", ReportType.OwnerRecertification)]
        public void Compile_ReportTypeName_SetsReportType(string reportTypeName, ReportType expectedReportType)
        {
            DynGraphqlQuery query = Compiler.Compile(CreateTemplate($"type={reportTypeName}"));

            Assert.That(query.ReportType, Is.EqualTo(expectedReportType));
        }

        [Test]
        public void Compile_UnknownReportTypeName_ThrowsSemanticException()
        {
            Assert.Throws<SemanticException>(() => Compiler.Compile(CreateTemplate("type=unknownreport")));
        }

        private static ReportTemplate CreateTemplate(string filter)
        {
            ReportTemplate template = new() { Filter = filter };
            template.ReportParams.ReportType = (int)ReportType.Rules;
            return template;
        }
    }
}
