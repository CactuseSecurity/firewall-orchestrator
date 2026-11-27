using AngleSharp.Dom;
using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.Middleware.Client;
using FWO.Services;
using FWO.Services.EventMediator;
using FWO.Services.EventMediator.Interfaces;
using FWO.Services.RuleTreeBuilder;
using FWO.Ui.Pages.NetworkModelling;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Security.Claims;

namespace FWO.Test
{
    /// <summary>
    /// Tests of the recertification popup of an application: when a recertification is possible, which findings
    /// prevent it, and the recertification itself. The production state is the one of <see cref="ModellingVarianceAnalysisTest"/>.
    /// </summary>
    [TestFixture]
    internal class UiRequestRecertPopupRecertFlowTest
    {
        private const int kAppId = 1;
        private const string kRecertText = "Recertify all connections of " + Placeholder.APPNAME;
        private const string kImplementedRuleName = "xxxFWOC2yyy";

        private sealed record UiMessage(string Title, string Text, bool Error);

        private static readonly ModellingService kService1 = new() { Id = 1, Name = "Service1", Port = 1000, PortEnd = 2000, ProtoId = 6 };
        private static readonly ModellingService kService2 = new() { Id = 2, Name = "Service2", Port = 4000, ProtoId = 6 };
        private static readonly ModellingAppServer kUnchangedServer = new() { Id = 1, Name = "AppServerUnchanged", Ip = "1.2.3.4" };

        /// <summary>
        /// Answers the variance analysis with the production state of <see cref="ModellingVarianceAnalysisTestApiConn"/>, which
        /// can be narrowed to a state without findings, and answers the queries of a recertification.
        /// </summary>
        private sealed class RecertApiConn : ModellingVarianceAnalysisTestApiConn
        {
            private readonly Stack<string> previousRoles = new();
            private bool recertificationStarted;

            public List<string> Queries { get; } = [];
            public HashSet<string> FailingQueries { get; } = [];
            public string ActiveRole { get; private set; } = "";
            public List<string> RecertifiedWithRoles { get; } = [];

            /// <summary>Production rules that remain; null keeps all rules of the base data.</summary>
            public Func<Rule, bool>? RuleFilter { get; set; }

            /// <summary>Connections regarded as deleted; null keeps the ones of the base data.</summary>
            public List<ModellingConnection>? DeletedConnections { get; set; }

            /// <summary>Modelled app zones of the application; null keeps the one of the base data.</summary>
            public List<ModellingAppZone>? AppZones { get; set; }

            public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null,
                QueryChunkingOptions? chunkingOptions = null)
            {
                Queries.Add(query);
                if (FailingQueries.Contains(query))
                {
                    throw new InvalidOperationException($"query failed: {query}");
                }
                object? answer = AnswerRecertQuery(query);
                if (answer != null)
                {
                    return (QueryResponseType)answer;
                }
                if (recertificationStarted)
                {
                    // the report of the recertification event cannot be generated here; the popup goes on without it
                    throw new AssertionException($"Unexpected query after the recertification started: {query}");
                }
                answer = AnswerPopupQuery(query);
                if (answer != null)
                {
                    return (QueryResponseType)answer;
                }
                QueryResponseType result = await base.SendQueryAsync<QueryResponseType>(query, variables, operationName, chunkingOptions);
                return result is List<Rule> rules && RuleFilter != null ? (QueryResponseType)(object)rules.Where(RuleFilter).ToList() : result;
            }

            private object? AnswerPopupQuery(string query)
            {
                return query switch
                {
                    _ when query == ExtRequestQueries.getLatestTicketId => new List<TicketId>(),
                    _ when query == RequestQueries.getExtStates => new List<WfExtState>
                    {
                        new() { Name = ExtStates.ExtReqDone.ToString(), StateId = 90 },
                        new() { Name = ExtStates.ExtReqRejected.ToString(), StateId = 91 }
                    },
                    _ when query == ModellingQueries.getDeletedConnections && DeletedConnections != null => DeletedConnections,
                    _ when query == ModellingQueries.getAppZonesByAppId && AppZones != null => AppZones,
                    _ => null
                };
            }

            private object? AnswerRecertQuery(string query)
            {
                if (query == RecertQueries.recertifyOwner)
                {
                    recertificationStarted = true;
                    RecertifiedWithRoles.Add(ActiveRole);
                    return new ReturnIdWrapper { ReturnIds = [new ReturnId { NewIdLong = 55 }] };
                }
                return query switch
                {
                    _ when query == OwnerQueries.setOwnerLastRecert => new ReturnId { AffectedRows = 1 },
                    _ when query == RecertQueries.recertifyRuleDirectly => new ReturnIdWrapper { ReturnIds = [new ReturnId { NewIdLong = 56 }] },
                    _ when query == ModellingQueries.addHistoryEntry => new ReturnIdWrapper { ReturnIds = [new ReturnId { NewIdLong = 57 }] },
                    _ => null
                };
            }

            /// <summary>
            /// Selects the first of the given roles the user has, and fails like the API would when there is none.
            /// </summary>
            public override void SetBestRole(ClaimsPrincipal user, List<string> targetRoleList)
            {
                string role = targetRoleList.FirstOrDefault(user.IsInRole)
                    ?? throw new UnauthorizedAccessException("the user has none of the required roles");
                previousRoles.Push(ActiveRole);
                ActiveRole = role;
            }

            public override void SwitchBack()
            {
                ActiveRole = previousRoles.TryPop(out string? previousRole) ? previousRole : "";
            }
        }

        private sealed class RecertTestContext(BunitContext context, SimulatedUserConfig userConfig) : IAsyncDisposable
        {
            public BunitContext Context { get; } = context;
            public SimulatedUserConfig UserConfig { get; } = userConfig;
            public List<UiMessage> Messages { get; } = [];
            public List<FwoOwner> RefreshedOwners { get; } = [];
            public List<bool> DisplayChanges { get; } = [];

            public ValueTask DisposeAsync()
            {
                return Context.DisposeAsync();
            }
        }

        /// <summary>
        /// A production state without findings: only the rule implementing the connection, no deleted connections and an
        /// app zone with the members of the production app zone.
        /// </summary>
        private static RecertApiConn CreateCleanApiConn()
        {
            return new RecertApiConn
            {
                RuleFilter = rule => rule.Name == kImplementedRuleName,
                DeletedConnections = [],
                AppZones =
                [
                    new ModellingAppZone
                    {
                        Id = 3,
                        Name = "AZ4711",
                        IdString = "AZ4711",
                        AppServers =
                        [
                            new() { Content = new ModellingAppServer { Id = 13, Name = "AppServerUnchanged", Ip = "1.2.3.4/32", IpEnd = "1.2.3.4/32" } },
                            new() { Content = new ModellingAppServer { Id = 16, Name = "AppServerOld", Ip = "1.0.0.0/32", IpEnd = "1.0.0.0/32" } }
                        ]
                    }
                ]
            };
        }

        private static RecertTestContext CreateRecertContext(ApiConnection apiConn, string[] loginRoles, string[]? executionRoles = null)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new AllowAllAuthStateProvider(loginRoles));
            SimulatedUserConfig userConfig = new()
            {
                ModIconify = false,
                ModIntegrationMode = ModIntegrationMode.FullyIntegrated,
                ModRecertText = kRecertText,
                ModNamingConvention = "{\"networkAreaRequired\":true,\"fixedPartLength\":4,\"freePartLength\":5,\"networkAreaPattern\":\"NA\",\"appRolePattern\":\"AR\"}",
                ModRolloutResolveServiceGroups = true,
                CreateAppZones = true,
                RuleRecognitionOption = "{\"nwRegardIp\":true,\"nwRegardName\":false,\"nwRegardGroupName\":false,\"nwResolveGroup\":false,\"nwSeparateGroupAnalysis\":true,\"svcRegardPortAndProt\":true,\"svcRegardName\":false,\"svcRegardGroupName\":false,\"svcResolveGroup\":true,\"svcSplitPortRanges\":true}",
                ModSpecUserAreas = "[{\"area_id\":1,\"use_in_src\":true,\"use_in_dst\":false}, {\"area_id\":3,\"use_in_src\":true,\"use_in_dst\":true}]",
                ModExtraConfigs = "[]",
                RecertificationPeriod = 365
            };
            userConfig.User.DbId = 3;
            userConfig.User.Dn = "uid=recertifier,ou=people,dc=example,dc=com";
            userConfig.User.Roles = [.. executionRoles ?? loginRoles];
            userConfig.User.Ownerships = [kAppId];
            context.Services.AddSingleton(apiConn);
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddSingleton(new MiddlewareClient("http://localhost/"));
            context.Services.AddSingleton<IRuleTreeBuilder, RuleTreeBuilder>();
            context.Services.AddSingleton<DomEventService>();
            context.Services.AddSingleton<IEventMediator>(new EventMediator());
            return new RecertTestContext(context, userConfig);
        }

        private static IRenderedComponent<RequestRecertPopup> RenderPopup(RecertTestContext test, ApiConnection apiConn, List<ModellingConnection> connections,
            bool canRecertify = true)
        {
            ModellingAppHandler appHandler = new(apiConn, test.UserConfig, new FwoOwner { Id = kAppId, Name = "App1", ExtAppId = "APP-1" }, DefaultInit.DoNothing, true)
            {
                Connections = connections
            };
            Action<Exception?, string, string, bool> displayMessage = (_, title, text, error) => test.Messages.Add(new(title, text, error));
            return test.Context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<CascadingValue<Action<Exception?, string, string, bool>>>(cascade => cascade
                    .Add(value => value.Value, displayMessage)
                    .AddChildContent<RequestRecertPopup>(popup => popup
                        .Add(p => p.Display, true)
                        .Add(p => p.DisplayChanged, value => test.DisplayChanges.Add(value))
                        .Add(p => p.AppHandler, appHandler)
                        .Add(p => p.CanRecertify, canRecertify)
                        .Add(p => p.RefreshParent, owner =>
                        {
                            test.RefreshedOwners.Add(owner);
                            return true;
                        }))))
                .FindComponent<RequestRecertPopup>();
        }

        /// <summary>The connection implemented by the rule xxxFWOC2yyy of the production state.</summary>
        private static ModellingConnection ImplementedConnection()
        {
            ModellingAppRole appRole3 = new() { Id = 3, Name = "AppRole3", IdString = "AR504711-003", AppServers = [new() { Content = kUnchangedServer }] };
            return new ModellingConnection
            {
                Id = 2,
                Name = "Conn2",
                SourceAppServers = [new() { Content = kUnchangedServer }],
                DestinationAppRoles = [new() { Content = appRole3 }],
                Services = [new() { Content = kService1 }]
            };
        }

        /// <summary>The connection whose rules differ from it in the production state.</summary>
        private static ModellingConnection DifferingConnection()
        {
            ModellingAppRole appRole1 = new()
            {
                Id = 1,
                Name = "AppRole1",
                IdString = "AR504711-001",
                AppServers = [new() { Content = kUnchangedServer }, new() { Content = new ModellingAppServer { Id = 2, Name = "AppServerNew1/32", Ip = "1.1.1.1", IpEnd = "1.1.1.1" } }]
            };
            ModellingAppRole appRole2 = new()
            {
                Id = 2,
                Name = "AppRole2",
                IdString = "AR504711-002",
                AppServers = [new() { Content = new ModellingAppServer { Id = 3, Name = "AppServerNew2", Ip = "2.2.2.2", IpEnd = "2.2.2.2" } }]
            };
            return new ModellingConnection
            {
                Id = 1,
                Name = "Conn1",
                SourceAppRoles = [new() { Content = appRole1 }],
                DestinationAppRoles = [new() { Content = appRole2 }],
                DestinationAppServers = [new() { Content = kUnchangedServer }],
                ServiceGroups = [new() { Content = new ModellingServiceGroup { Id = 1, Name = "SvcGrp1", Services = [new() { Content = kService2 }] } }],
                Services = [new() { Content = kService1 }]
            };
        }

        /// <summary>The connection without a rule in the production state.</summary>
        private static ModellingConnection MissingConnection()
        {
            return new ModellingConnection
            {
                Id = 3,
                Name = "Conn3",
                SourceAppServers = [new() { Content = kUnchangedServer }],
                DestinationAppServers = [new() { Content = kUnchangedServer }],
                Services = [new() { Content = kService2 }]
            };
        }

        private static List<IElement> RecertButtons(IRenderedComponent<RequestRecertPopup> popup, SimulatedUserConfig userConfig)
        {
            return [.. popup.FindAll("button").Where(button => button.TextContent.Trim() == userConfig.GetText("recertify"))];
        }

        private static void WaitForAnalysis(IRenderedComponent<RequestRecertPopup> popup, RecertApiConn apiConn)
        {
            popup.WaitForAssertion(() =>
            {
                Assert.That(apiConn.Queries, Does.Contain(ModellingQueries.getAppZonesByAppId));
                Assert.That(popup.FindComponent<FWO.Ui.Shared.InProgress>().Instance.Display, Is.False);
            });
        }

        [Test]
        public async Task CleanApplicationCanBeRecertified()
        {
            RecertApiConn apiConn = CreateCleanApiConn();
            await using RecertTestContext test = CreateRecertContext(apiConn, [Roles.Recertifier]);

            IRenderedComponent<RequestRecertPopup> popup = RenderPopup(test, apiConn, [ImplementedConnection()]);

            popup.WaitForAssertion(() =>
            {
                Assert.That(popup.Markup, Does.Contain("Recertify all connections of"));
                Assert.That(popup.Markup, Does.Not.Contain(test.UserConfig.GetText("U9029")));
                Assert.That(RecertButtons(popup, test.UserConfig).Single().HasAttribute("disabled"), Is.False);
            });
        }

        [Test]
        public async Task RecertifyingRecordsTheRecertificationAndCloses()
        {
            RecertApiConn apiConn = CreateCleanApiConn();
            await using RecertTestContext test = CreateRecertContext(apiConn, [Roles.Recertifier]);
            IRenderedComponent<RequestRecertPopup> popup = RenderPopup(test, apiConn, [ImplementedConnection()]);
            popup.WaitForAssertion(() => Assert.That(RecertButtons(popup, test.UserConfig), Has.Count.EqualTo(1)));

            await popup.InvokeAsync(() => RecertButtons(popup, test.UserConfig)[0].Click());

            string expectedText = test.UserConfig.GetText("U4002").Replace(Placeholder.APPNAME, "App1")
                .Replace(Placeholder.CONN_NUMBER, "1").Replace(Placeholder.RULE_NUMBER, "1");
            popup.WaitForAssertion(() =>
            {
                Assert.That(apiConn.RecertifiedWithRoles, Is.EqualTo(new List<string> { Roles.Recertifier }));
                Assert.That(apiConn.Queries.Count(query => query == RecertQueries.recertifyRuleDirectly), Is.EqualTo(1), "the implementing rule is recertified");
                Assert.That(apiConn.Queries, Does.Contain(OwnerQueries.setOwnerLastRecert));
                Assert.That(apiConn.Queries, Does.Contain(ModellingQueries.addHistoryEntry));
                Assert.That(test.RefreshedOwners.Single().LastRecertId, Is.EqualTo(55));
                Assert.That(test.RefreshedOwners.Single().NextRecertDate, Is.Not.Null);
                Assert.That(test.Messages, Does.Contain(new UiMessage(test.UserConfig.GetText("recertification_ok"), expectedText, false)));
                Assert.That(test.DisplayChanges, Is.EqualTo(new List<bool> { false }));
                Assert.That(apiConn.ActiveRole, Is.Empty, "the recertification role is given up again");
            });
        }

        [Test]
        public async Task RecertifyingReportsAFailedRecertification()
        {
            RecertApiConn apiConn = CreateCleanApiConn();
            apiConn.FailingQueries.Add(RecertQueries.recertifyOwner);
            await using RecertTestContext test = CreateRecertContext(apiConn, [Roles.Recertifier]);
            IRenderedComponent<RequestRecertPopup> popup = RenderPopup(test, apiConn, [ImplementedConnection()]);
            popup.WaitForAssertion(() => Assert.That(RecertButtons(popup, test.UserConfig), Has.Count.EqualTo(1)));

            await popup.InvokeAsync(() => RecertButtons(popup, test.UserConfig)[0].Click());

            popup.WaitForAssertion(() =>
            {
                Assert.That(test.Messages, Has.Some.Matches<UiMessage>(message => message.Title == test.UserConfig.GetText("recertification_error") && message.Error));
                Assert.That(test.RefreshedOwners, Is.Empty);
            });
        }

        [Test]
        public async Task RecertifyingReportsALoginWithoutRecertificationRole()
        {
            RecertApiConn apiConn = CreateCleanApiConn();
            // the execution mode offers the recertifier role, the login itself does not have it
            await using RecertTestContext test = CreateRecertContext(apiConn, [Roles.Modeller], [Roles.Recertifier]);
            IRenderedComponent<RequestRecertPopup> popup = RenderPopup(test, apiConn, [ImplementedConnection()]);
            popup.WaitForAssertion(() => Assert.That(RecertButtons(popup, test.UserConfig), Has.Count.EqualTo(1)));

            await popup.InvokeAsync(() => RecertButtons(popup, test.UserConfig)[0].Click());

            popup.WaitForAssertion(() =>
            {
                Assert.That(test.Messages, Has.Some.Matches<UiMessage>(message => message.Title == test.UserConfig.GetText("recertify") && message.Error));
                Assert.That(apiConn.RecertifiedWithRoles, Is.Empty);
                Assert.That(test.DisplayChanges, Is.Empty, "the popup stays open");
            });
        }

        [Test]
        public async Task RecertifierWithoutOwnerAssignmentSeesADisabledButton()
        {
            RecertApiConn apiConn = CreateCleanApiConn();
            await using RecertTestContext test = CreateRecertContext(apiConn, [Roles.Recertifier]);

            IRenderedComponent<RequestRecertPopup> popup = RenderPopup(test, apiConn, [ImplementedConnection()], canRecertify: false);

            popup.WaitForAssertion(() =>
            {
                IElement button = RecertButtons(popup, test.UserConfig).Single();
                Assert.That(button.HasAttribute("disabled"), Is.True);
                Assert.That(button.ParentElement?.GetAttribute("title"), Is.EqualTo(test.UserConfig.PureLine("C9032")));
            });
        }

        [Test]
        public async Task UserWithoutRecertifierRoleSeesADisabledButton()
        {
            RecertApiConn apiConn = CreateCleanApiConn();
            await using RecertTestContext test = CreateRecertContext(apiConn, [Roles.Auditor]);

            IRenderedComponent<RequestRecertPopup> popup = RenderPopup(test, apiConn, [ImplementedConnection()]);

            popup.WaitForAssertion(() =>
            {
                IElement button = RecertButtons(popup, test.UserConfig).Single();
                Assert.That(button.HasAttribute("disabled"), Is.True);
                Assert.That(button.ParentElement?.GetAttribute("title"), Is.EqualTo(test.UserConfig.PureLine("C9031")));
            });
        }

        [Test]
        public async Task ApplicationWithoutModelledConnectionsCannotBeRecertified()
        {
            RecertApiConn apiConn = CreateCleanApiConn();
            await using RecertTestContext test = CreateRecertContext(apiConn, [Roles.Recertifier]);
            ModellingConnection providedInterface = ImplementedConnection();
            providedInterface.IsInterface = true;

            IRenderedComponent<RequestRecertPopup> popup = RenderPopup(test, apiConn, [providedInterface]);

            popup.WaitForAssertion(() =>
            {
                Assert.That(popup.Markup, Does.Contain(test.UserConfig.GetText("U9027")));
                Assert.That(RecertButtons(popup, test.UserConfig), Is.Empty);
            });
        }

        [Test]
        public async Task MissingAndDifferingImplementationsPreventTheRecertification()
        {
            RecertApiConn apiConn = CreateCleanApiConn();
            apiConn.RuleFilter = null;
            await using RecertTestContext test = CreateRecertContext(apiConn, [Roles.Recertifier]);

            IRenderedComponent<RequestRecertPopup> popup = RenderPopup(test, apiConn, [DifferingConnection(), ImplementedConnection(), MissingConnection()]);
            WaitForAnalysis(popup, apiConn);

            popup.WaitForAssertion(() =>
            {
                Assert.That(popup.Markup, Does.Contain(test.UserConfig.GetText("U9029")));
                Assert.That(popup.Markup, Does.Contain(test.UserConfig.GetText("connections_not_implemented")).And.Contain("Conn3"));
                Assert.That(popup.Markup, Does.Contain(test.UserConfig.GetText("connections_with_diffs")).And.Contain("Conn1"));
                Assert.That(popup.Markup, Does.Contain(test.UserConfig.GetText("rules_not_modelled")).And.Contain("NonModelledRule"));
                Assert.That(RecertButtons(popup, test.UserConfig), Is.Empty);
            });
        }

        [Test]
        public async Task RulesOfDeletedConnectionsPreventTheRecertification()
        {
            RecertApiConn apiConn = CreateCleanApiConn();
            apiConn.RuleFilter = null;
            apiConn.DeletedConnections = null;
            await using RecertTestContext test = CreateRecertContext(apiConn, [Roles.Recertifier]);

            IRenderedComponent<RequestRecertPopup> popup = RenderPopup(test, apiConn, [ImplementedConnection()]);
            WaitForAnalysis(popup, apiConn);

            popup.WaitForAssertion(() =>
            {
                Assert.That(popup.Markup, Does.Contain(test.UserConfig.GetText("rules_for_deleted_conns")).And.Contain("FWOC4"));
                Assert.That(RecertButtons(popup, test.UserConfig), Is.Empty);
            });
        }

        [Test]
        public async Task DifferingAppZonePreventsTheRecertification()
        {
            RecertApiConn apiConn = CreateCleanApiConn();
            apiConn.AppZones = null;
            await using RecertTestContext test = CreateRecertContext(apiConn, [Roles.Recertifier]);

            IRenderedComponent<RequestRecertPopup> popup = RenderPopup(test, apiConn, [ImplementedConnection()]);
            WaitForAnalysis(popup, apiConn);

            popup.WaitForAssertion(() =>
            {
                Assert.That(popup.Markup, Does.Contain(test.UserConfig.GetText("app_zone")));
                Assert.That(popup.Markup, Does.Contain(test.UserConfig.GetText("az_differing_on")));
                Assert.That(RecertButtons(popup, test.UserConfig), Is.Empty);
            });
        }
    }
}
