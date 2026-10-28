using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Data.Report;
using FWO.Middleware.Server.Controllers;
using FWO.Middleware.Server;
using FWO.Middleware.Server.Requests;
using FWO.Report;
using FWO.Report.Filter;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;
using System.Reflection;
using System.Security.Cryptography;

namespace FWO.Test
{
    [TestFixture]
    internal class ReportControllerTest
    {
        private static readonly List<string> kResolvedReportView = ["resolved"];
        private static readonly List<string> kTechnicalReportView = ["technical"];
        private static readonly List<string> kIpWithFilterSyntax = ["((((1.1.1.1"];
        private static readonly List<string> kFirstSourceIpPath = ["rule-filter.source-ip[0]"];
        private static readonly List<string> kRuleFilterPath = ["rule-filter"];
        private const string kServiceNameWithFilterSyntax = "web (alt) or src=1.2.3.4 \"x\" \\";
        private const string kLongestIpRange = "ffff:ffff:ffff:ffff:ffff:ffff:ffff:fff0-ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff";

        [Test]
        public async Task ConvertParameters_BuildsTemplateWithDeviceAndRuleFilters()
        {
            ReportControllerTestApiConnection apiConnection = new()
            {
                Managements =
                [
                    new ManagementSelect
                    {
                        Id = 1,
                        Name = "mgmt-1",
                        Visible = true,
                        Devices =
                        [
                            new DeviceSelect { Id = 10, Name = "gw-10", Visible = true },
                            new DeviceSelect { Id = 11, Name = "gw-11", Visible = false }
                        ]
                    },
                    new ManagementSelect
                    {
                        Id = 2,
                        Name = "mgmt-2",
                        Visible = true,
                        Devices =
                        [
                            new DeviceSelect { Id = 20, Name = "gw-20", Visible = true }
                        ]
                    }
                ],
                IpProtocols =
                [
                    new IpProtocol { Id = 17, Name = "udp" }
                ]
            };
            ReportController controller = CreateController(apiConnection);

            ReportTemplate template = await InvokePrivateAsync<ReportTemplate>(controller, "ConvertParameters", new ReportGetParameters
            {
                ApiReportType = "rules",
                ApiReportView = kResolvedReportView,
                ApiDeviceFilter = new ApiDeviceFilter
                {
                    ManagementIds = [1],
                    DeviceIds = [20]
                },
                ApiRuleFilter = new ApiRuleFilter
                {
                    SourceIps = ["10.1.0.0/16"],
                    DestinationIps = ["10.2.0.0/16"],
                    Ips = ["10.3.0.0/16"],
                    Services =
                    [
                        new ApiService
                        {
                            Name = "http",
                            Protocol = 17,
                            Port = 80
                        }
                    ]
                },
                Action = "allow",
                Active = true
            });

            Assert.That(template.ReportParams.ReportType, Is.EqualTo(5));
            Assert.That(template.ReportParams.DeviceFilter.Managements, Has.Count.EqualTo(2));
            Assert.That(template.ReportParams.DeviceFilter.Managements[0].Id, Is.EqualTo(1));
            Assert.That(template.ReportParams.DeviceFilter.Managements[0].Selected, Is.True);
            Assert.That(template.ReportParams.DeviceFilter.Managements[0].Devices[0].Selected, Is.True);
            Assert.That(template.ReportParams.DeviceFilter.Managements[0].Devices[1].Selected, Is.False);
            Assert.That(template.ReportParams.DeviceFilter.Managements[1].Id, Is.EqualTo(2));
            Assert.That(template.ReportParams.DeviceFilter.Managements[1].Selected, Is.True);
            Assert.That(template.ReportParams.DeviceFilter.Managements[1].Devices[0].Selected, Is.True);
            Assert.That(template.Filter, Is.EqualTo(
                "(src=10.1.0.0/16 or dst=10.2.0.0/16 or src=10.3.0.0/16 or dst=10.3.0.0/16) and (svc=\"http\" and protocol=udp and port=80) and action=\"allow\" and disabled=False"));
            Assert.That(apiConnection.DeviceQueryCount, Is.EqualTo(1));
            Assert.That(apiConnection.ProtocolQueryCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Get_RejectsIpWithFilterSyntaxBeforeAnyQuery()
        {
            ReportController controller = CreateController(new ReportControllerTestApiConnection());

            ActionResult<string> result = await controller.Get(new ReportGetParameters
            {
                ApiRuleFilter = new ApiRuleFilter { SourceIps = kIpWithFilterSyntax }
            });

            ValidationProblemDetails problem = GetValidationProblem(result);
            Assert.That(problem.Errors.Keys, Is.EquivalentTo(kFirstSourceIpPath));
        }

        [Test]
        public async Task Get_RejectsRequestWhoseFilterExceedsTheCompilerLimit()
        {
            List<string> longestRanges = Enumerable.Repeat(kLongestIpRange, ReportGetParametersValidator.kMaxFilterValues).ToList();
            ReportController controller = CreateController(new ReportControllerTestApiConnection());

            ActionResult<string> result = await controller.Get(new ReportGetParameters
            {
                ApiRuleFilter = new ApiRuleFilter { SourceIps = longestRanges, DestinationIps = longestRanges, Ips = longestRanges }
            });

            ValidationProblemDetails problem = GetValidationProblem(result);
            Assert.That(problem.Errors.Keys, Is.EquivalentTo(kRuleFilterPath));
            Assert.That(problem.Errors["rule-filter"][0], Does.Contain(Compiler.kMaxFilterLength.ToString()));
        }

        [Test]
        public async Task ConvertParameters_QuotesServiceNameAndActionSoTheyStayOneValue()
        {
            ReportController controller = CreateController(new ReportControllerTestApiConnection());

            ReportTemplate template = await InvokePrivateAsync<ReportTemplate>(controller, "ConvertParameters", new ReportGetParameters
            {
                ApiRuleFilter = new ApiRuleFilter
                {
                    Services = [new ApiService { Name = kServiceNameWithFilterSyntax }]
                },
                Action = "drop or (src=1.2.3.4"
            });
            DynGraphqlQuery query = Compiler.Compile(template);

            Assert.That(template.Filter, Is.EqualTo("(svc=\"web (alt) or src=1.2.3.4 \\\"x\\\" \\\\\") and action=\"drop or (src=1.2.3.4\""));
            Assert.That(query.QueryVariables.Values.OfType<string>().Any(value => value.Contains(kServiceNameWithFilterSyntax)), Is.True);
            Assert.That(query.QueryVariables.Values.OfType<string>().Any(value => value.Contains("drop or (src=1.2.3.4")), Is.True);
        }

        [Test]
        public void QuoteFilterValue_EscapesQuotesAndBackslashes()
        {
            Assert.That(ReportController.QuoteFilterValue("a\"b\\c"), Is.EqualTo("\"a\\\"b\\\\c\""));
        }

        [Test]
        public void ConstructReportType_MapsSpecialCases()
        {
            Assert.Multiple(() =>
            {
                Assert.That(InvokePrivateStatic<int>("ConstructReportType", "owners", kResolvedReportView), Is.EqualTo(51));
                Assert.That(InvokePrivateStatic<int>("ConstructReportType", "variance", kTechnicalReportView), Is.EqualTo(23));
                Assert.That(InvokePrivateStatic<int>("ConstructReportType", "unknown", new List<string>()), Is.EqualTo(1));
            });
        }

        private static ReportController CreateController(ApiConnection apiConnection)
        {
            RSA rsa = RSA.Create(2048);
            return new ReportController(new JwtWriter(new RsaSecurityKey(rsa)), [], apiConnection);
        }

        private static ValidationProblemDetails GetValidationProblem(ActionResult<string> result)
        {
            BadRequestObjectResult badRequest = result.Result as BadRequestObjectResult
                ?? throw new AssertionException($"Expected a bad request, got {result.Result?.GetType().Name ?? result.Value}");
            return badRequest.Value as ValidationProblemDetails
                ?? throw new AssertionException("Expected validation problem details.");
        }

        private static async Task<T> InvokePrivateAsync<T>(object instance, string methodName, params object?[] arguments)
        {
            MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
            return await (Task<T>)method.Invoke(instance, arguments)!;
        }

        private static T InvokePrivateStatic<T>(string methodName, params object?[] arguments)
        {
            MethodInfo method = typeof(ReportController).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(typeof(ReportController).FullName, methodName);
            return (T)method.Invoke(null, arguments)!;
        }

        private sealed class ReportControllerTestApiConnection : SimulatedApiConnection
        {
            public List<ManagementSelect> Managements { get; set; } = [];
            public List<IpProtocol> IpProtocols { get; set; } = [];
            public int DeviceQueryCount { get; private set; }
            public int ProtocolQueryCount { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (typeof(QueryResponseType) == typeof(List<ManagementSelect>) && query == DeviceQueries.getDevicesByManagement)
                {
                    DeviceQueryCount++;
                    return Task.FromResult((QueryResponseType)(object)Managements);
                }

                if (typeof(QueryResponseType) == typeof(List<IpProtocol>) && query == StmQueries.getIpProtocols)
                {
                    ProtocolQueryCount++;
                    return Task.FromResult((QueryResponseType)(object)IpProtocols);
                }

                throw new AssertionException($"Unexpected query: {query} for type {typeof(QueryResponseType).Name}");
            }
        }
    }
}
