using FWO.Data.Middleware;
using FWO.Middleware.Server.Requests;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ReportGetParametersValidatorTest
    {
        private static readonly List<string> kValidIps = ["10.1.2.3", "10.1.0.0/16", "10.1.0.1-10.1.0.9", "2001:db8::/32", "::1", "192.168.0.0/255.255.255.0"];
        private static readonly List<string> kInvalidIps = ["", "((((1.1.1.1", "1.1.1.1)", "10.1.0.1 - 10.1.0.9", "src=1.1.1.1", "not 1.1.1.1", "host-a", "10.1.2.300", "\"10.1.2.3\""];
        private static readonly List<string> kResolvedReportView = ["resolved"];
        private static readonly List<int> kDeviceIds = [1, 2];
        private static readonly List<string> kSourceIpWithBracket = ["(1.1.1.1"];
        private static readonly List<string> kDestinationIpWithBlank = ["1.1.1.1 or src=2.2.2.2"];
        private static readonly List<string?> kIpListWithNull = [null];
        private static readonly List<string?> kReportViewWithNull = [null];
        private static readonly List<string> kMultipleErrorPaths =
        [
            "action",
            "rule-filter.source-ip[0]",
            "rule-filter.destination-ip[0]",
            "rule-filter.service[0].name",
            "rule-filter.service[1].port",
            "rule-filter.service[2]"
        ];
        private static readonly List<string> kTooManyValuesPaths = ["report-view", "device-filter.management-ids", "device-filter.gateway-ids", "rule-filter.service"];
        private static readonly List<string> kTooLongTextPaths = ["report-type", "report-view[0]", "action", "rule-filter.service[0].name"];
        private static readonly List<string> kNullObjectPaths = ["device-filter", "rule-filter"];
        private static readonly List<string> kNullListPaths =
        [
            "report-view",
            "device-filter.management-ids",
            "device-filter.gateway-ids",
            "rule-filter.ip",
            "rule-filter.source-ip",
            "rule-filter.destination-ip",
            "rule-filter.service"
        ];

        [Test]
        public void Validate_AcceptsDefaultRequest()
        {
            Assert.That(ReportGetParametersValidator.Validate(new ReportGetParameters()), Is.Empty);
        }

        [Test]
        public void Validate_AcceptsCompleteValidRequest()
        {
            ReportGetParameters parameters = new()
            {
                ApiReportType = "rules",
                ApiReportView = kResolvedReportView,
                ApiDeviceFilter = new ApiDeviceFilter { ManagementIds = kDeviceIds, DeviceIds = kDeviceIds },
                ApiRuleFilter = new ApiRuleFilter
                {
                    Ips = kValidIps,
                    SourceIps = kValidIps,
                    DestinationIps = kValidIps,
                    Services = [new ApiService { Name = "http", Protocol = 6, Port = 80 }, new ApiService { Port = 0 }, new ApiService { Port = 65535, Protocol = 255 }]
                },
                Action = "accept",
                Active = true
            };

            Assert.That(ReportGetParametersValidator.Validate(parameters), Is.Empty);
        }

        [Test]
        public void Validate_RejectsNullBody()
        {
            Dictionary<string, string[]> errors = ReportGetParametersValidator.Validate(null);

            Assert.That(errors.Keys.Single(), Is.EqualTo("body"));
        }

        [TestCaseSource(nameof(kInvalidIps))]
        public void Validate_RejectsValuesThatAreNoIpAddressOrRange(string invalidIp)
        {
            List<string> ips = [invalidIp];
            ReportGetParameters parameters = new() { ApiRuleFilter = new ApiRuleFilter { Ips = ips } };

            Dictionary<string, string[]> errors = ReportGetParametersValidator.Validate(parameters);

            Assert.That(errors.Keys.Single(), Is.EqualTo("rule-filter.ip[0]"));
            Assert.That(errors["rule-filter.ip[0]"][0], Does.Contain("CIDR network"));
        }

        [Test]
        public void Validate_RejectsNullIpValue()
        {
            ReportGetParameters parameters = new() { ApiRuleFilter = new ApiRuleFilter { SourceIps = kIpListWithNull! } };

            Dictionary<string, string[]> errors = ReportGetParametersValidator.Validate(parameters);

            Assert.That(errors.Keys.Single(), Is.EqualTo("rule-filter.source-ip[0]"));
        }

        [Test]
        public void Validate_AcceptsMaximumNumberOfIpsAndRejectsOneMore()
        {
            List<string> maximumIps = Enumerable.Repeat("10.0.0.1", ReportGetParametersValidator.kMaxFilterValues).ToList();
            List<string> tooManyIps = Enumerable.Repeat("10.0.0.1", ReportGetParametersValidator.kMaxFilterValues + 1).ToList();

            Dictionary<string, string[]> maximumErrors = ReportGetParametersValidator.Validate(new ReportGetParameters { ApiRuleFilter = new ApiRuleFilter { SourceIps = maximumIps } });
            Dictionary<string, string[]> tooManyErrors = ReportGetParametersValidator.Validate(new ReportGetParameters { ApiRuleFilter = new ApiRuleFilter { SourceIps = tooManyIps } });

            Assert.That(maximumErrors, Is.Empty);
            Assert.That(tooManyErrors.Keys.Single(), Is.EqualTo("rule-filter.source-ip"));
            Assert.That(tooManyErrors["rule-filter.source-ip"][0], Does.Contain(ReportGetParametersValidator.kMaxFilterValues.ToString()));
        }

        [Test]
        public void Validate_RejectsTooManyServicesReportViewsAndDeviceIds()
        {
            List<ApiService> tooManyServices = Enumerable.Range(0, ReportGetParametersValidator.kMaxFilterValues + 1).Select(_ => new ApiService()).ToList();
            List<string> tooManyViews = Enumerable.Repeat("resolved", ReportGetParametersValidator.kMaxFilterValues + 1).ToList();
            List<int> tooManyIds = Enumerable.Range(1, ReportGetParametersValidator.kMaxDeviceFilterIds + 1).ToList();
            ReportGetParameters parameters = new()
            {
                ApiReportView = tooManyViews,
                ApiDeviceFilter = new ApiDeviceFilter { ManagementIds = tooManyIds, DeviceIds = tooManyIds },
                ApiRuleFilter = new ApiRuleFilter { Services = tooManyServices }
            };

            Dictionary<string, string[]> errors = ReportGetParametersValidator.Validate(parameters);

            Assert.That(errors.Keys, Is.EquivalentTo(kTooManyValuesPaths));
        }

        [Test]
        public void Validate_AcceptsMaximumTextLengthAndRejectsLongerTexts()
        {
            string maximumText = new('a', ReportGetParametersValidator.kMaxFilterTextLength);
            string tooLongText = new('a', ReportGetParametersValidator.kMaxFilterTextLength + 1);
            List<string> tooLongViews = [tooLongText];
            ReportGetParameters validParameters = new()
            {
                ApiReportType = maximumText,
                Action = maximumText,
                ApiRuleFilter = new ApiRuleFilter { Services = [new ApiService { Name = maximumText }] }
            };
            ReportGetParameters invalidParameters = new()
            {
                ApiReportType = tooLongText,
                ApiReportView = tooLongViews,
                Action = tooLongText,
                ApiRuleFilter = new ApiRuleFilter { Services = [new ApiService { Name = tooLongText }] }
            };

            Dictionary<string, string[]> validErrors = ReportGetParametersValidator.Validate(validParameters);
            Dictionary<string, string[]> invalidErrors = ReportGetParametersValidator.Validate(invalidParameters);

            Assert.That(validErrors, Is.Empty);
            Assert.That(invalidErrors.Keys, Is.EquivalentTo(kTooLongTextPaths));
        }

        [Test]
        public void Validate_RejectsNullReportType()
        {
            ReportGetParameters parameters = new() { ApiReportType = null! };

            Dictionary<string, string[]> errors = ReportGetParametersValidator.Validate(parameters);

            Assert.That(errors.Keys.Single(), Is.EqualTo("report-type"));
        }

        [Test]
        public void Validate_RejectsNullReportView()
        {
            ReportGetParameters parameters = new() { ApiReportView = kReportViewWithNull! };

            Dictionary<string, string[]> errors = ReportGetParametersValidator.Validate(parameters);

            Assert.That(errors.Keys.Single(), Is.EqualTo("report-view[0]"));
        }

        [TestCase(-1, null)]
        [TestCase(65536, null)]
        [TestCase(null, -1)]
        [TestCase(null, 256)]
        public void Validate_RejectsPortOrProtocolOutOfRange(int? port, int? protocol)
        {
            ReportGetParameters parameters = new()
            {
                ApiRuleFilter = new ApiRuleFilter { Services = [new ApiService { Port = port, Protocol = protocol }] }
            };

            Dictionary<string, string[]> errors = ReportGetParametersValidator.Validate(parameters);

            string expectedPath = port != null ? "rule-filter.service[0].port" : "rule-filter.service[0].protocol";
            Assert.That(errors.Keys.Single(), Is.EqualTo(expectedPath));
        }

        [Test]
        public void Validate_ReportsAllErrorsTogether()
        {
            ReportGetParameters parameters = new()
            {
                Action = new string('a', ReportGetParametersValidator.kMaxFilterTextLength + 1),
                ApiRuleFilter = new ApiRuleFilter
                {
                    SourceIps = kSourceIpWithBracket,
                    DestinationIps = kDestinationIpWithBlank,
                    Services =
                    [
                        new ApiService { Name = new string('s', ReportGetParametersValidator.kMaxFilterTextLength + 1) },
                        new ApiService { Port = 70000 },
                        null!
                    ]
                }
            };

            Dictionary<string, string[]> errors = ReportGetParametersValidator.Validate(parameters);

            Assert.That(errors.Keys, Is.EquivalentTo(kMultipleErrorPaths));
        }

        [Test]
        public void Validate_RejectsNullObjects()
        {
            ReportGetParameters parameters = new() { ApiDeviceFilter = null!, ApiRuleFilter = null! };

            Dictionary<string, string[]> errors = ReportGetParametersValidator.Validate(parameters);

            Assert.That(errors.Keys, Is.EquivalentTo(kNullObjectPaths));
        }

        [Test]
        public void Validate_RejectsNullLists()
        {
            ReportGetParameters parameters = new()
            {
                ApiReportView = null!,
                ApiDeviceFilter = new ApiDeviceFilter { ManagementIds = null!, DeviceIds = null! },
                ApiRuleFilter = new ApiRuleFilter { Ips = null!, SourceIps = null!, DestinationIps = null!, Services = null! }
            };

            Dictionary<string, string[]> errors = ReportGetParametersValidator.Validate(parameters);

            Assert.That(errors.Keys, Is.EquivalentTo(kNullListPaths));
        }
    }
}
