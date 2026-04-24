using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Middleware.Server;
using FWO.Middleware.Server.Jobs;
using FWO.Report;
using FWO.Report.Filter;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System.Reflection;
using System.Security.Cryptography;

namespace FWO.Test
{
    [TestFixture]
    [NonParallelizable]
    internal class ReportJobTest
    {
        private object? originalConfigFileData;

        private sealed class ReportJobApiConnection : ApiConnection
        {
            internal string? LastQuery { get; private set; }
            internal object? LastVariables { get; private set; }
            internal int GetDevicesByManagementCalls { get; private set; }

            public override void SetAuthHeader(string jwt) { }
            public override void SetRole(string role) { }
            public override void SetBestRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList) { }
            public override void SetProperRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList) { }
            public override void SwitchBack() { }

            public override Task<ApiResponse<QueryResponseType>> SendQuerySafeAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
            {
                throw new NotImplementedException();
            }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
            {
                LastQuery = query;
                LastVariables = variables;
                if (typeof(QueryResponseType) == typeof(List<ManagementSelect>) && query == DeviceQueries.getDevicesByManagement)
                {
                    GetDevicesByManagementCalls++;
                    return Task.FromResult((QueryResponseType)(object)new List<ManagementSelect>
                    {
                        new()
                        {
                            Id = 1,
                            Name = "mgmt-1",
                            Visible = true,
                            Devices =
                            [
                                new() { Id = 11, Name = "dev-1", Visible = true },
                                new() { Id = 12, Name = "dev-2", Visible = true }
                            ]
                        }
                    });
                }
                if (typeof(QueryResponseType) == typeof(object) && query == ReportQueries.addGeneratedReport)
                {
                    return Task.FromResult((QueryResponseType)(object)new object());
                }
                throw new NotImplementedException();
            }

            public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler, GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null, string? operationName = null)
            {
                throw new NotImplementedException();
            }

            public override void DisposeSubscriptions<T>() { }
            protected override void Dispose(bool disposing) { }
        }

        [SetUp]
        public void SetUp()
        {
            originalConfigFileData = typeof(FWO.Config.File.ConfigFile)
                .GetProperty("Data", BindingFlags.Static | BindingFlags.NonPublic)!
                .GetValue(null);
        }

        [TearDown]
        public void TearDown()
        {
            typeof(FWO.Config.File.ConfigFile)
                .GetProperty("Data", BindingFlags.Static | BindingFlags.NonPublic)!
                .SetValue(null, originalConfigFileData);
        }

        private static MethodInfo GetPrivateStaticMethod(string name)
        {
            return typeof(ReportJob).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(typeof(ReportJob).FullName, name);
        }

        private static MethodInfo GetPrivateInstanceMethod(string name)
        {
            return typeof(ReportJob).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(ReportJob).FullName, name);
        }

        private static ReportJob CreateReportJob(ApiConnection? apiConnection = null)
        {
            SetApiServerUri("http://unit-test");
            return new ReportJob(apiConnection ?? new ReportJobApiConnection(), new JwtWriter(new RsaSecurityKey(RSA.Create())));
        }

        private static void SetApiServerUri(string apiServerUri)
        {
            Type configFileType = typeof(FWO.Config.File.ConfigFile);
            Type? configFileDataType = configFileType.GetNestedType("ConfigFileData", BindingFlags.NonPublic);
            object configData = Activator.CreateInstance(configFileDataType ?? throw new MissingMemberException(configFileType.FullName, "ConfigFileData"))
                ?? throw new InvalidOperationException("Could not create config file data.");
            configFileDataType.GetProperty("ApiServerUri", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
                .SetValue(configData, apiServerUri);
            configFileType.GetProperty("Data", BindingFlags.Static | BindingFlags.NonPublic)!
                .SetValue(null, configData);
        }

        [Test]
        public void RoundDown_RoundsToMinuteBoundary()
        {
            MethodInfo roundDown = GetPrivateStaticMethod("RoundDown");
            DateTime input = new(2026, 3, 27, 10, 15, 42, 987, DateTimeKind.Utc);

            DateTime rounded = (DateTime)roundDown.Invoke(null, [input, TimeSpan.FromMinutes(1)])!;

            ClassicAssert.AreEqual(new DateTime(2026, 3, 27, 10, 15, 0, DateTimeKind.Utc), rounded);
        }

        [Test]
        public async Task WriteReportFile_CsvCapableReport_WritesRequestedFormats()
        {
            MethodInfo writeReportFile = GetPrivateStaticMethod("WriteReportFile");
            TestReport report = new(ReportType.TicketChangeReport, detailedView: false);
            ReportFile reportFile = new();
            List<FileFormat> fileFormats =
            [
                new() { Name = GlobalConst.kCsv },
                new() { Name = GlobalConst.kHtml },
                new() { Name = GlobalConst.kPdf },
                new() { Name = GlobalConst.kJson }
            ];

            await (Task)writeReportFile.Invoke(null, [report, fileFormats, reportFile])!;

            ClassicAssert.AreEqual("json-content", reportFile.Json);
            ClassicAssert.AreEqual("csv-content", reportFile.Csv);
            ClassicAssert.AreEqual("html-content", reportFile.Html);
            ClassicAssert.AreEqual("pdf-content", reportFile.Pdf);
            Assert.That(report.PdfInputs, Has.Count.EqualTo(1));
            ClassicAssert.AreEqual("html-content", report.PdfInputs[0]);
            Assert.That(reportFile.GenerationDateEnd, Is.Not.EqualTo(default(DateTime)));
        }

        [Test]
        public async Task WriteReportFile_DetailedWorkflowReport_SkipsCsvExport()
        {
            MethodInfo writeReportFile = GetPrivateStaticMethod("WriteReportFile");
            TestReport report = new(ReportType.TicketChangeReport, detailedView: true);
            ReportFile reportFile = new();
            List<FileFormat> fileFormats =
            [
                new() { Name = GlobalConst.kCsv },
                new() { Name = GlobalConst.kJson }
            ];

            await (Task)writeReportFile.Invoke(null, [report, fileFormats, reportFile])!;

            ClassicAssert.AreEqual("json-content", reportFile.Json);
            ClassicAssert.IsTrue(string.IsNullOrEmpty(reportFile.Csv));
            Assert.That(report.PdfInputs, Is.Empty);
        }

        [Test]
        public void WriteReportFile_UnsupportedFormat_Throws()
        {
            MethodInfo writeReportFile = GetPrivateStaticMethod("WriteReportFile");
            TestReport report = new(ReportType.TicketChangeReport, detailedView: false);
            ReportFile reportFile = new();

            Assert.ThrowsAsync<NotSupportedException>(async () =>
                await (Task)writeReportFile.Invoke(null, [report, new List<FileFormat> { new() { Name = "xml" } }, reportFile])!);
        }

        [Test]
        public async Task AdaptDeviceFilter_WithoutSelectedDevices_SelectsAllLoadedDevices()
        {
            MethodInfo adaptDeviceFilter = GetPrivateStaticMethod("AdaptDeviceFilter");
            ReportParams reportParams = new() { ReportType = (int)ReportType.Rules };
            ReportJobApiConnection apiConnection = new();

            await (Task)adaptDeviceFilter.Invoke(null, [reportParams, apiConnection])!;

            Assert.That(apiConnection.GetDevicesByManagementCalls, Is.EqualTo(1));
            Assert.That(reportParams.DeviceFilter.Managements, Has.Count.EqualTo(1));
            Assert.That(reportParams.DeviceFilter.Managements[0].Selected, Is.True);
            Assert.That(reportParams.DeviceFilter.Managements[0].Devices.All(device => device.Selected), Is.True);
        }

        [Test]
        public async Task AdaptDeviceFilter_WithSelectedDevice_DoesNotReloadDevices()
        {
            MethodInfo adaptDeviceFilter = GetPrivateStaticMethod("AdaptDeviceFilter");
            ReportParams reportParams = new()
            {
                ReportType = (int)ReportType.Rules,
                DeviceFilter = new DeviceFilter(
                [
                    new()
                    {
                        Id = 1,
                        Devices = [new() { Id = 11, Selected = true }]
                    }
                ])
            };
            ReportJobApiConnection apiConnection = new();

            await (Task)adaptDeviceFilter.Invoke(null, [reportParams, apiConnection])!;

            Assert.That(apiConnection.GetDevicesByManagementCalls, Is.EqualTo(0));
            Assert.That(reportParams.DeviceFilter.Managements[0].Devices[0].Selected, Is.True);
        }

        [Test]
        public async Task SaveReportToArchive_PersistsAllExpectedFields()
        {
            MethodInfo saveReportToArchive = GetPrivateStaticMethod("SaveReportToArchive");
            ReportJobApiConnection apiConnection = new();
            ReportFile reportFile = new()
            {
                Name = "scheduled-report",
                GenerationDateStart = new DateTime(2026, 4, 21, 10, 0, 0, DateTimeKind.Utc),
                GenerationDateEnd = new DateTime(2026, 4, 21, 10, 5, 0, DateTimeKind.Utc),
                OwningUserId = 50,
                TemplateId = 17,
                Type = (int)ReportType.TicketReport,
                Pdf = "pdf",
                Csv = "csv",
                Html = "html",
                Json = "json"
            };

            await (Task)saveReportToArchive.Invoke(null, [reportFile, "description", apiConnection])!;

            Assert.That(apiConnection.LastQuery, Is.EqualTo(ReportQueries.addGeneratedReport));
            Assert.That(GetAnonymousProperty<string>(apiConnection.LastVariables!, "report_name"), Is.EqualTo("scheduled-report"));
            Assert.That(GetAnonymousProperty<int>(apiConnection.LastVariables!, "report_owner_id"), Is.EqualTo(50));
            Assert.That(GetAnonymousProperty<int>(apiConnection.LastVariables!, "report_template_id"), Is.EqualTo(17));
            Assert.That(GetAnonymousProperty<int>(apiConnection.LastVariables!, "report_type"), Is.EqualTo((int)ReportType.TicketReport));
            Assert.That(GetAnonymousProperty<string>(apiConnection.LastVariables!, "report_pdf"), Is.EqualTo("pdf"));
            Assert.That(GetAnonymousProperty<string>(apiConnection.LastVariables!, "description"), Is.EqualTo("description"));
        }

        [Test]
        public async Task ProcessScheduledReport_InactiveSchedule_DoesNothing()
        {
            MethodInfo processScheduledReport = GetPrivateInstanceMethod("ProcessScheduledReport");
            ReportJob reportJob = CreateReportJob();
            DateTime startTime = new(2026, 4, 21, 10, 0, 0);
            ReportSchedule reportSchedule = new()
            {
                Active = false,
                StartTime = startTime,
                RepeatInterval = SchedulerInterval.Days,
                RepeatOffset = 1
            };

            await (Task)processScheduledReport.Invoke(reportJob, [reportSchedule, startTime.AddMinutes(5), CancellationToken.None])!;

            Assert.That(reportSchedule.StartTime, Is.EqualTo(startTime));
        }

        [Test]
        public async Task ProcessScheduledReport_OverdueSchedule_AdvancesToNextFutureRun()
        {
            MethodInfo processScheduledReport = GetPrivateInstanceMethod("ProcessScheduledReport");
            ReportJob reportJob = CreateReportJob();
            ReportSchedule reportSchedule = new()
            {
                Active = true,
                StartTime = new DateTime(2026, 4, 18, 10, 0, 0),
                RepeatInterval = SchedulerInterval.Days,
                RepeatOffset = 2
            };
            DateTime currentTimeRounded = new(2026, 4, 21, 10, 5, 0);

            await (Task)processScheduledReport.Invoke(reportJob, [reportSchedule, currentTimeRounded, CancellationToken.None])!;

            Assert.That(reportSchedule.StartTime, Is.EqualTo(new DateTime(2026, 4, 22, 10, 0, 0)));
        }

        private static T GetAnonymousProperty<T>(object obj, string propertyName)
        {
            return (T)(obj.GetType().GetProperty(propertyName)?.GetValue(obj)
                ?? throw new MissingMemberException(obj.GetType().FullName, propertyName));
        }

        private sealed class TestReport : ReportBase
        {
            internal List<string> PdfInputs { get; } = [];

            internal TestReport(ReportType reportType, bool detailedView)
                : base(new DynGraphqlQuery(""), new SimulatedUserConfig(), reportType)
            {
                ReportData = new()
                {
                    WorkflowFilter = new WorkflowFilter { DetailedView = detailedView }
                };
            }

            public override Task Generate(int elementsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
            {
                return Task.CompletedTask;
            }

            public override string ExportToCsv()
            {
                return "csv-content";
            }

            public override string ExportToJson()
            {
                return "json-content";
            }

            public override string ExportToHtml()
            {
                return "html-content";
            }

            public override string SetDescription()
            {
                return "";
            }

            public override Task<string?> ToPdf(string html)
            {
                PdfInputs.Add(html);
                return Task.FromResult<string?>("pdf-content");
            }
        }
    }
}
