using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Report;
using FWO.Report.Filter;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace FWO.Test
{
    [TestFixture]
    public class RecertEventReportTest
    {
        [SetUp]
        public void SetupTranslations()
        {
            SimulatedUserConfig.DummyTranslate.TryAdd("by", "by");
            SimulatedUserConfig.DummyTranslate.TryAdd("recertification", "Recertification");
            SimulatedUserConfig.DummyTranslate.TryAdd("recertified_rules", "Recertified rules");
            SimulatedUserConfig.DummyTranslate.TryAdd("RecertificationEvent", "Recertification Event");
            SimulatedUserConfig.DummyTranslate.TryAdd("RecertEventReport", "Recertification Event Report");
        }

        [Test]
        public void RecertificateOwner_GetRecertText_UsesDateAndRecertifier()
        {
            OwnerConnectionReport ownerReport = BuildOwnerReport();

            string text = RecertificateOwner.GetRecertText(ownerReport, new SimulatedUserConfig());

            Assert.That(text, Is.EqualTo("Recertification 03.06.2026 11:45 by certifier.user"));
        }

        [Test]
        public void RecertificateOwner_ExportToHtml_RendersOwnerRecertSection()
        {
            RecertificateOwner report = new(new DynGraphqlQuery(""), new SimulatedUserConfig(), ReportType.RecertificationEvent)
            {
                ReportData = new() { OwnerData = new List<OwnerConnectionReport> { BuildOwnerReport() } }
            };

            string html = report.ExportToHtml();

            Assert.That(html, Does.Contain("Recertification Event"));
            Assert.That(html, Does.Contain("Recertification 03.06.2026 11:45 by certifier.user"));
            Assert.That(html, Does.Contain("Application One (APP-1)"));
        }

        [Test]
        public void ReportRecertEvent_ExportToHtml_RendersOwnerSectionBeforeRulesHeadline()
        {
            ReportRecertEvent report = new(new DynGraphqlQuery(""), new SimulatedUserConfig(), ReportType.RecertEventReport)
            {
                ReportData = new()
                {
                    OwnerData = new List<OwnerConnectionReport> { BuildOwnerReport() },
                    ManagementData = new List<ManagementReport>()
                }
            };

            string html = report.ExportToHtml();

            Assert.That(html, Does.Contain("Recertification Event Report"));
            Assert.That(html.IndexOf("Recertification 03.06.2026 11:45 by certifier.user", StringComparison.Ordinal),
                Is.LessThan(html.IndexOf("Recertified rules", StringComparison.Ordinal)));
        }

        [Test]
        public async Task ReportRecertEvent_GetRecertification_DeserializesStoredOwnerData()
        {
            List<OwnerConnectionReport> ownerReports = new() { BuildOwnerReport() };
            RecertEventApiConnection apiConnection = new(JsonSerializer.Serialize(ownerReports));

            List<OwnerConnectionReport> result = await ReportRecertEvent.GetRecertification(123, apiConnection);

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(result[0].Owner.Name, Is.EqualTo("Application One"));
                Assert.That(apiConnection.LastQuery, Is.EqualTo(ReportQueries.getGeneratedReport));
                Assert.That(apiConnection.LastVariables, Is.Not.Null);
            });
        }

        [Test]
        public async Task ReportRecertEvent_GetRecertification_ReturnsEmptyListWithoutReportId()
        {
            List<OwnerConnectionReport> ownerReports = new() { BuildOwnerReport() };
            RecertEventApiConnection apiConnection = new(JsonSerializer.Serialize(ownerReports));

            List<OwnerConnectionReport> result = await ReportRecertEvent.GetRecertification(null, apiConnection);

            Assert.That(result, Is.Empty);
            Assert.That(apiConnection.QueryCount, Is.EqualTo(0));
        }

        private static OwnerConnectionReport BuildOwnerReport()
        {
            return new()
            {
                Owner = new()
                {
                    Id = 1,
                    Name = "Application One",
                    ExtAppId = "APP-1",
                    LastRecertified = new DateTime(2026, 6, 3, 11, 45, 0),
                    LastRecertifierDn = "cn=certifier.user,ou=users,dc=test,dc=local"
                }
            };
        }

        private sealed class RecertEventApiConnection(string? reportJson) : SimulatedApiConnection
        {
            public int QueryCount { get; private set; }
            public string LastQuery { get; private set; } = "";
            public object? LastVariables { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null,
                string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                ++QueryCount;
                LastQuery = query;
                LastVariables = variables;

                if (typeof(QueryResponseType) == typeof(List<ReportFile>))
                {
                    List<ReportFile> reports = new() { new() { Id = 123, Json = reportJson } };
                    return Task.FromResult((QueryResponseType)(object)reports);
                }

                throw new NotImplementedException();
            }
        }
    }
}
