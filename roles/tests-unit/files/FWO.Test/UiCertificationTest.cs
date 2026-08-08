using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Middleware.Client;
using FWO.Recert;
using FWO.Services.RuleTreeBuilder;
using FWO.Ui.Pages;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using NSubstitute;
using System.Reflection;
using System.Security.Claims;

namespace FWO.Test
{
    [TestFixture]
    internal class UiCertificationTest
    {
        [SetUp]
        public void SetUp()
        {
            SimulatedUserConfig.DummyTranslate["execute_selected"] = "Execute selected";
            SimulatedUserConfig.DummyTranslate["report_data_fetch"] = "Report data fetch";
            SimulatedUserConfig.DummyTranslate["no_device_selected"] = "No device selected";
            SimulatedUserConfig.DummyTranslate["E1001"] = "No device selected";
            SimulatedUserConfig.DummyTranslate["E1003"] = "Canceled";
            SimulatedUserConfig.DummyTranslate["generate_report"] = "Generate report";
            SimulatedUserConfig.DummyTranslate["E4002"] = "No rules found";
            SimulatedUserConfig.DummyTranslate["E4001"] = "Comment required";
            SimulatedUserConfig.DummyTranslate["E9104"] = "You are not allowed to execute selected rules.";
            SimulatedUserConfig.DummyTranslate["recerts_executed"] = "Recerts executed ";
            SimulatedUserConfig.DummyTranslate["decerts_executed"] = "Decerts executed ";
            SimulatedUserConfig.DummyTranslate["load_rules"] = "Load rules";
            SimulatedUserConfig.DummyTranslate["stop_fetching"] = "Stop fetching";
            SimulatedUserConfig.DummyTranslate["comment"] = "Comment";
            SimulatedUserConfig.DummyTranslate["ok"] = "OK";
            SimulatedUserConfig.DummyTranslate["cancel"] = "Cancel";
            SimulatedUserConfig.DummyTranslate["add_comment"] = "Add comment";
        }

        private static Certification CreateComponent(
            SimulatedUserConfig userConfig,
            SimulatedApiConnection? apiConnection = null,
            Task<AuthenticationState>? authenticationStateTask = null)
        {
            Certification component = new();
            SetMember(component, "userConfig", userConfig);
            SetMember(component, "apiConnection", apiConnection ?? new NoopCertificationApiConnection());
            SetMember(component, "middlewareClient", new MiddlewareClient("http://localhost/"));
            SetMember(component, "ruleTreeBuilder", Substitute.For<IRuleTreeBuilder>());
            SetMember(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((_, _, _, _) => { }));
            SetMember(component, "authenticationStateTask", authenticationStateTask ?? Task.FromResult(new AuthenticationState(new ClaimsPrincipal())));
            return component;
        }

        private static void SetMember<T>(object instance, string memberName, T value)
        {
            Type type = instance.GetType();
            PropertyInfo? property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
            {
                property.SetValue(instance, value);
                return;
            }

            FieldInfo? field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(instance, value);
                return;
            }

            throw new MissingMemberException(type.FullName, memberName);
        }

        private static T GetMember<T>(object instance, string memberName)
        {
            Type type = instance.GetType();
            PropertyInfo? property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
            {
                return (T)property.GetValue(instance)!;
            }

            FieldInfo? field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                return (T)field.GetValue(instance)!;
            }

            throw new MissingMemberException(type.FullName, memberName);
        }

        private static object? InvokePrivate(object instance, string methodName, params object?[] args)
        {
            MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
            return method.Invoke(instance, args);
        }

        private static async Task InvokePrivateTask(object instance, string methodName, params object?[] args)
        {
            Task task = (Task)(InvokePrivate(instance, methodName, args)
                ?? throw new InvalidOperationException($"{methodName} returned null task."));
            await task;
        }

        [Test]
        public void CanWriteSelectedOwner_RespectsAdminAndOwnerAssignments()
        {
            SimulatedUserConfig adminUserConfig = new();
            adminUserConfig.User.Roles = new List<string> { Roles.Admin };
            Certification adminComponent = CreateComponent(adminUserConfig);
            SetMember(adminComponent, "selectedOwner", new FwoOwner { Id = 7, Name = "Owner A" });
            SetMember(adminComponent, "recertifiableOwnerIds", new HashSet<int>());

            bool adminCanWrite = (bool)InvokePrivate(adminComponent, "CanWriteSelectedOwner")!;

            SimulatedUserConfig recertifierUserConfig = new();
            recertifierUserConfig.User.Roles = new List<string> { Roles.Recertifier };
            Certification recertifierComponent = CreateComponent(recertifierUserConfig);
            SetMember(recertifierComponent, "selectedOwner", new FwoOwner { Id = 7, Name = "Owner A" });
            SetMember(recertifierComponent, "recertifiableOwnerIds", new HashSet<int> { 8 });

            bool recertifierCanWrite = (bool)InvokePrivate(recertifierComponent, "CanWriteSelectedOwner")!;

            Assert.Multiple(() =>
            {
                Assert.That(adminCanWrite, Is.True);
                Assert.That(recertifierCanWrite, Is.False);
            });
        }

        [Test]
        public void ShowNoRecertifiableOwnersHint_OnlyShowsForRecertifierWithoutAssignments()
        {
            SimulatedUserConfig userConfig = new();
            userConfig.User.Roles = new List<string> { Roles.Recertifier };
            Certification component = CreateComponent(userConfig);
            SetMember(component, "collectedOwnerships", new List<FwoOwner> { new() { Id = 1, Name = "Owner A" } });
            SetMember(component, "recertifiableOwnerIds", new HashSet<int>());

            bool showHint = (bool)InvokePrivate(component, "ShowNoRecertifiableOwnersHint")!;

            Assert.That(showHint, Is.True);
        }

        [Test]
        public void PrepareReportParams_UsesSelectedOwnerAndLookAheadDays()
        {
            SimulatedUserConfig userConfig = new();
            Certification component = CreateComponent(userConfig);
            DeviceFilter deviceFilter = new();
            FwoOwner selectedOwner = new() { Id = 42, Name = "Owner A" };

            SetMember(component, "deviceFilter", deviceFilter);
            SetMember(component, "selectedOwner", selectedOwner);
            SetMember(component, "recertLookAheadDays", 21);

            ReportParams reportParams = (ReportParams)InvokePrivate(component, "prepareReportParams")!;

            Assert.Multiple(() =>
            {
                Assert.That(reportParams.ReportType, Is.EqualTo((int)ReportType.Recertification));
                Assert.That(reportParams.DeviceFilter, Is.SameAs(deviceFilter));
                Assert.That(reportParams.RecertFilter.RecertOwnerList, Is.EqualTo(new List<int> { 42 }));
                Assert.That(reportParams.RecertFilter.RecertShowAnyMatch, Is.True);
                Assert.That(reportParams.RecertFilter.RecertificationDisplayPeriod, Is.EqualTo(21));
            });
        }

        [Test]
        public void PrepareReportParams_LeavesOwnerListEmptyWhenNothingIsSelected()
        {
            Certification component = CreateComponent(new SimulatedUserConfig());
            SetMember(component, "recertLookAheadDays", 9);

            ReportParams reportParams = (ReportParams)InvokePrivate(component, "prepareReportParams")!;

            Assert.Multiple(() =>
            {
                Assert.That(reportParams.RecertFilter.RecertOwnerList, Is.Empty);
                Assert.That(reportParams.RecertFilter.RecertificationDisplayPeriod, Is.EqualTo(9));
            });
        }

        [Test]
        public void AnalyzeSelected_CollectsOnlyRecertAndDecertRules()
        {
            SimulatedUserConfig userConfig = new();
            Certification component = CreateComponent(userConfig);
            Rule recertRule = CreateRule(1, recert: true);
            Rule decertRule = CreateRule(2, recert: false, toBeRemoved: true);
            Rule untouchedRule = CreateRule(3, recert: false);

            SetMember(component, "managementsReport", new List<ManagementReport>
            {
                new()
                {
                    Id = 11,
                    Rulebases = CreateRulebases(recertRule, decertRule, untouchedRule)
                }
            });

            InvokePrivate(component, "AnalyzeSelected");

            List<Rule> certifications = GetMember<List<Rule>>(component, "Certifications");
            Assert.Multiple(() =>
            {
                Assert.That(certifications, Has.Count.EqualTo(2));
                Assert.That(certifications, Does.Contain(recertRule));
                Assert.That(certifications, Does.Contain(decertRule));
                Assert.That(certifications, Does.Not.Contain(untouchedRule));
            });
        }

        [Test]
        public void PostProcessReport_StopsAtFirstContainedRule()
        {
            SimulatedUserConfig userConfig = new();
            Certification component = CreateComponent(userConfig);

            SetMember(component, "managementsReport", new List<ManagementReport>
            {
                new()
                {
                    Id = 11,
                    Rulebases = CreateRulebases(CreateRule(1, recert: true))
                }
            });

            InvokePrivate(component, "postProcessReport");

            bool rulesFound = GetMember<bool>(component, "rulesFound");
            Assert.That(rulesFound, Is.True);
        }

        [Test]
        public async Task PostProcessReport_LeavesRulesFoundFalseForEmptyReport()
        {
            Certification component = CreateComponent(new SimulatedUserConfig());

            SetMember(component, "managementsReport", new List<ManagementReport>());

            InvokePrivate(component, "postProcessReport");

            bool rulesFound = GetMember<bool>(component, "rulesFound");
            Assert.That(rulesFound, Is.False);
        }

        [Test]
        public async Task GenerateRecertificationReport_ShowsWarningWhenNoDeviceIsSelected()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            Certification component = CreateComponent(new SimulatedUserConfig());
            SetMember(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((exception, title, message, isError) =>
            {
                messages.Add((exception, title, message, isError));
            }));

            await InvokePrivateTask(component, "GenerateRecertificationReport");

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("No device selected"));
                Assert.That(messages[0].Message, Is.EqualTo("No device selected"));
                Assert.That(GetMember<bool>(component, "processing"), Is.False);
                Assert.That(GetMember<bool>(component, "rulesFound"), Is.False);
            });
        }

        [Test]
        public async Task OnInitializedAsync_LoadsOwnersDevicesAndDisplayPeriod()
        {
            RecordingCertificationApiConnection apiConnection = new();
            apiConnection.Owners = new List<FwoOwner>
            {
                new() { Id = 1, Name = "Owner A" }
            };
            apiConnection.DeviceManagements = new List<ManagementSelect>
            {
                new() { Id = 4, Name = "Mgmt A" }
            };

            SimulatedUserConfig userConfig = new();
            userConfig.User.Roles = new List<string> { Roles.Admin };
            userConfig.RecertificationDisplayPeriod = 17;
            await using BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new AllowAllAuthStateProvider(Roles.Admin));
            context.Services.AddSingleton<DomEventService>();
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddSingleton<MiddlewareClient>(new MiddlewareClient("http://localhost/"));
            context.Services.AddSingleton<IRuleTreeBuilder>(Substitute.For<IRuleTreeBuilder>());

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<CascadingValue<Action<Exception?, string, string, bool>>>(child => child
                    .Add(p => p.Value, (_, _, _, _) => { })
                    .AddChildContent<Certification>()));
            Certification component = wrapper.FindComponent<Certification>().Instance;

            wrapper.WaitForAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(apiConnection.CountQuery(OwnerQueries.getOwners), Is.EqualTo(2));
                    Assert.That(apiConnection.CountQuery(DeviceQueries.getDevicesByManagement), Is.EqualTo(1));
                    Assert.That(GetMember<List<FwoOwner>>(component, "ownerList"), Has.Count.EqualTo(1));
                    Assert.That(GetMember<List<FwoOwner>>(component, "collectedOwnerships"), Has.Count.EqualTo(1));
                    Assert.That(GetMember<DeviceFilter>(component, "deviceFilter").Managements, Has.Count.EqualTo(1));
                    Assert.That(GetMember<int>(component, "recertLookAheadDays"), Is.EqualTo(17));
                });
            });
        }

        [Test]
        public async Task OnInitializedAsync_ShowsErrorWhenLoadingFails()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            FailingCertificationApiConnection apiConnection = new();
            SimulatedUserConfig userConfig = new();
            Certification component = CreateComponent(userConfig, apiConnection, CreateAuthenticationTask(Roles.Admin));
            SetMember(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((exception, title, message, isError) =>
            {
                messages.Add((exception, title, message, isError));
            }));

            await InvokePrivateTask(component, "OnInitializedAsync");

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("Object Fetch"));
                Assert.That(messages[0].IsError, Is.True);
                Assert.That(GetMember<List<FwoOwner>>(component, "ownerList"), Is.Empty);
            });
        }

        [Test]
        public async Task CollectOwnerships_AdminUsesAllOwnersAndSelectsFirst()
        {
            RecordingCertificationApiConnection apiConnection = new();
            apiConnection.Owners = new List<FwoOwner>
            {
                new() { Id = 7, Name = "Owner A" },
                new() { Id = 9, Name = "Owner B" }
            };

            SimulatedUserConfig userConfig = new();
            userConfig.User.Roles = new List<string> { Roles.Admin };
            Certification component = CreateComponent(userConfig, apiConnection, CreateAuthenticationTask(Roles.Admin));

            await InvokePrivateTask(component, "CollectOwnerships");

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.CountQuery(OwnerQueries.getOwners), Is.EqualTo(1));
                Assert.That(GetMember<List<FwoOwner>>(component, "collectedOwnerships"), Has.Count.EqualTo(2));
                Assert.That(GetMember<FwoOwner?>(component, "selectedOwner")?.Id, Is.EqualTo(7));
                Assert.That(GetMember<HashSet<int>>(component, "recertifiableOwnerIds"), Is.EquivalentTo(new HashSet<int> { 7, 9 }));
            });
        }

        [Test]
        public async Task CollectOwnerships_ResolvesClaimBasedOwnersForRecertifier()
        {
            RecordingCertificationApiConnection apiConnection = new();
            apiConnection.EditableOwners = new List<FwoOwner>
            {
                new() { Id = 7, Name = "Owner A" },
                new() { Id = 8, Name = "Owner B" }
            };

            SimulatedUserConfig userConfig = new();
            userConfig.User.Roles = new List<string> { Roles.Recertifier };
            userConfig.User.Ownerships = new List<int>();
            userConfig.User.RecertOwnerships = new List<int>();
            Certification component = CreateComponent(userConfig, apiConnection, CreateAuthenticationTaskWithClaims(
                new Claim("x-hasura-editable-owners", "{7,8}"),
                new Claim("x-hasura-recertifiable-owners", "{8}")));

            await InvokePrivateTask(component, "CollectOwnerships");

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.CountQuery(OwnerQueries.getEditableOwners), Is.EqualTo(1));
                Assert.That(GetMember<List<FwoOwner>>(component, "collectedOwnerships"), Has.Count.EqualTo(2));
                Assert.That(GetMember<FwoOwner?>(component, "selectedOwner")?.Id, Is.EqualTo(8));
                Assert.That(GetMember<HashSet<int>>(component, "recertifiableOwnerIds"), Is.EquivalentTo(new HashSet<int> { 8 }));
            });
        }

        [Test]
        public async Task CollectOwnerships_DoesNotQueryEditableOwnersWhenNoReadableOwnersExist()
        {
            RecordingCertificationApiConnection apiConnection = new();
            SimulatedUserConfig userConfig = new();
            userConfig.User.Roles = new List<string> { Roles.Recertifier };
            userConfig.User.Ownerships = new List<int>();
            userConfig.User.RecertOwnerships = new List<int>();
            Certification component = CreateComponent(userConfig, apiConnection, CreateAuthenticationTask(Roles.Recertifier));

            await InvokePrivateTask(component, "CollectOwnerships");

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.CountQuery(OwnerQueries.getEditableOwners), Is.EqualTo(0));
                Assert.That(GetMember<List<FwoOwner>>(component, "collectedOwnerships"), Is.Empty);
                Assert.That(GetMember<FwoOwner?>(component, "selectedOwner"), Is.Null);
            });
        }

        [Test]
        public void CancelGeneration_CancelsTokenAndShowsMessage()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            Certification component = CreateComponent(new SimulatedUserConfig());
            SetMember(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((exception, title, message, isError) =>
            {
                messages.Add((exception, title, message, isError));
            }));

            InvokePrivate(component, "CancelGeneration");

            CancellationTokenSource tokenSource = GetMember<CancellationTokenSource>(component, "tokenSource");
            Assert.Multiple(() =>
            {
                Assert.That(tokenSource.IsCancellationRequested, Is.True);
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("Report data fetch"));
                Assert.That(messages[0].Message, Is.EqualTo("Canceled"));
            });
        }

        [Test]
        public void RequestExecuteSelected_SetsCommentModeForAllowedOwner()
        {
            Certification component = CreateExecutableComponent(out _, out _);

            InvokePrivate(component, "RequestExecuteSelected");

            bool addCommentMode = GetMember<bool>(component, "AddCommentMode");
            Assert.That(addCommentMode, Is.True);
        }

        [Test]
        public void RequestExecuteSelected_ShowsErrorWhenOwnerCannotBeWritten()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            Certification component = CreateComponent(new SimulatedUserConfig());
            SetMember(component, "selectedOwner", new FwoOwner { Id = 7, Name = "Owner A" });
            SetMember(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((exception, title, message, isError) =>
            {
                messages.Add((exception, title, message, isError));
            }));

            InvokePrivate(component, "RequestExecuteSelected");

            Assert.Multiple(() =>
            {
                Assert.That(GetMember<bool>(component, "AddCommentMode"), Is.False);
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("Execute selected"));
                Assert.That(messages[0].Message, Is.EqualTo("You are not allowed to execute selected rules."));
            });
        }

        [Test]
        public async Task ExecuteSelected_ShowsErrorWhenOwnerCannotBeWritten()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            RecordingCertificationApiConnection apiConnection = new();
            Certification component = CreateExecutableComponent(out _, out _, apiConnection, messages);
            SetMember(component, "selectedOwner", new FwoOwner { Id = 99, Name = "Other Owner" });
            SetMember(component, "recertifiableOwnerIds", new HashSet<int> { 7 });

            await InvokePrivateTask(component, "ExecuteSelected");

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.Queries, Is.Empty);
                Assert.That(messages.Any(entry => entry.Title == "Execute selected" && entry.Message == "You are not allowed to execute selected rules."), Is.True);
            });
        }

        [Test]
        public void Cancel_HidesCommentPopup()
        {
            Certification component = CreateExecutableComponent(out _, out _);
            SetMember(component, "AddCommentMode", true);

            InvokePrivate(component, "Cancel");

            Assert.That(GetMember<bool>(component, "AddCommentMode"), Is.False);
        }

        [Test]
        public async Task ExecuteSelected_ShowsErrorWhenCommentIsRequired()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            RecordingCertificationApiConnection apiConnection = new();
            Certification component = CreateExecutableComponent(out SimulatedUserConfig userConfig, out FwoOwner selectedOwner, apiConnection, messages);
            userConfig.CommentRequired = true;
            SetMember(component, "AddCommentMode", true);
            SetMember(component, "actComment", "   ");

            await InvokePrivateTask(component, "ExecuteSelected");

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.Queries, Is.Empty);
                Assert.That(messages.Any(entry => entry.Message == "Comment required"), Is.True);
                Assert.That(GetMember<bool>(component, "AddCommentMode"), Is.True);
            });
        }

        [Test]
        public async Task DoRecerts_WithNoCertificationsWritesZeroSummary()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            RecordingCertificationApiConnection apiConnection = new();
            Certification component = CreateExecutableComponent(out _, out _, apiConnection, messages);
            SetMember(component, "Certifications", new List<Rule>());

            await InvokePrivateTask(component, "DoRecerts");

            Dictionary<int, List<string>> deleteList = GetMember<Dictionary<int, List<string>>>(component, "deleteList");
            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.Queries, Is.Empty);
                Assert.That(deleteList, Is.Empty);
                Assert.That(messages.Any(entry => entry.Title == "Execute selected" && entry.Message == "Recerts executed 0, Decerts executed 0"), Is.True);
            });
        }

        [Test]
        public async Task RecertifyRuleAndCollectDeletion_LeavesDeleteListEmptyWhenRecertsRemainOpen()
        {
            RecordingCertificationApiConnection apiConnection = new();
            apiConnection.OpenRecertsByRuleId[2] = new List<Recertification> { new() };
            Certification component = CreateExecutableComponent(out _, out _, apiConnection);
            SetMember(component, "actComment", "needs action");
            SetMember(component, "deleteList", new Dictionary<int, List<string>>());
            Rule rule = CreateRule(2, recert: false, toBeRemoved: true);
            RecertHandler recertHandler = new(apiConnection, new SimulatedUserConfig());

            bool result = await (Task<bool>)InvokePrivate(component, "RecertifyRuleAndCollectDeletion", recertHandler, rule)!;

            Dictionary<int, List<string>> deleteList = GetMember<Dictionary<int, List<string>>>(component, "deleteList");
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(apiConnection.CountQuery(RecertQueries.getOpenRecertsForRule), Is.EqualTo(1));
                Assert.That(deleteList, Is.Empty);
            });
        }

        [Test]
        public async Task RecertifyRuleAndCollectDeletion_ReturnsFalseWhenRecertificationFails()
        {
            RecordingCertificationApiConnection apiConnection = new();
            apiConnection.RecertifyRowsByRuleId[3] = 0;
            Certification component = CreateExecutableComponent(out _, out _, apiConnection);
            SetMember(component, "actComment", "needs action");
            SetMember(component, "deleteList", new Dictionary<int, List<string>>());
            Rule rule = CreateRule(3, recert: false, toBeRemoved: true);
            RecertHandler recertHandler = new(apiConnection, new SimulatedUserConfig());

            bool result = await (Task<bool>)InvokePrivate(component, "RecertifyRuleAndCollectDeletion", recertHandler, rule)!;

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(apiConnection.CountQuery(RecertQueries.getOpenRecertsForRule), Is.EqualTo(0));
                Assert.That(GetMember<Dictionary<int, List<string>>>(component, "deleteList"), Is.Empty);
            });
        }

        [Test]
        public async Task ExecuteSelected_ProcessesCertificationsAndAddsDeletionCandidates()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            RecordingCertificationApiConnection apiConnection = new();
            apiConnection.OpenRecertsByRuleId[2] = new List<Recertification>();
            Certification component = CreateExecutableComponent(out SimulatedUserConfig userConfig, out _, apiConnection, messages);
            userConfig.CommentRequired = false;
            SetMember(component, "actComment", "needs\"sanitizing\"");
            SetMember(component, "managementsReport", new List<ManagementReport>
            {
                new()
                {
                    Id = 11,
                    Rulebases = CreateRulebases(
                        CreateRule(1, recert: true),
                        CreateRule(2, recert: false, toBeRemoved: true))
                }
            });

            await InvokePrivateTask(component, "ExecuteSelected");

            Dictionary<int, List<string>> deleteList = GetMember<Dictionary<int, List<string>>>(component, "deleteList");
            Assert.Multiple(() =>
            {
                Assert.That(messages.Any(entry => entry.Message == "Input text..."), Is.True);
                Assert.That(messages.Any(entry => entry.Message == "Recerts executed 1, Decerts executed 1"), Is.True);
                Assert.That(messages.Any(entry => entry.Message == "No device selected"), Is.True);
                Assert.That(apiConnection.CountQuery(RecertQueries.recertify), Is.EqualTo(2));
                Assert.That(apiConnection.CountQuery(RecertQueries.prepareNextRecertification), Is.EqualTo(1));
                Assert.That(apiConnection.CountQuery(RecertQueries.getOpenRecertsForRule), Is.EqualTo(1));
                Assert.That(deleteList, Has.Count.EqualTo(1));
                Assert.That(deleteList.Values.Single(), Is.EqualTo(new List<string> { "rule-2" }));
            });
        }

        private static Task<AuthenticationState> CreateAuthenticationTask(params string[] roles)
        {
            ClaimsIdentity identity = new(
                roles.Select(role => new Claim(ClaimTypes.Role, role)),
                authenticationType: "Test",
                nameType: ClaimTypes.Name,
                roleType: ClaimTypes.Role);
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
        }

        private static Task<AuthenticationState> CreateAuthenticationTaskWithClaims(params Claim[] claims)
        {
            ClaimsIdentity identity = new(
                claims,
                authenticationType: "Test",
                nameType: ClaimTypes.Name,
                roleType: ClaimTypes.Role);
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
        }

        private static Certification CreateExecutableComponent(
            out SimulatedUserConfig userConfig,
            out FwoOwner selectedOwner,
            SimulatedApiConnection? apiConnection = null,
            List<(Exception? Exception, string Title, string Message, bool IsError)>? messages = null)
        {
            userConfig = new SimulatedUserConfig
            {
                CommentRequired = false
            };
            userConfig.User.Roles = new List<string> { Roles.Recertifier };
            selectedOwner = new FwoOwner
            {
                Id = 7,
                Name = "Owner A",
                RecertInterval = 14
            };
            Certification component = CreateComponent(userConfig, apiConnection, CreateAuthenticationTask(Roles.Recertifier));
            SetMember(component, "selectedOwner", selectedOwner);
            SetMember(component, "recertifiableOwnerIds", new HashSet<int> { selectedOwner.Id });
            SetMember(component, "collectedOwnerships", new List<FwoOwner> { selectedOwner });
            SetMember(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((exception, title, message, isError) =>
            {
                messages?.Add((exception, title, message, isError));
            }));
            return component;
        }

        private static RulebaseReport[] CreateRulebases(params Rule[] rules)
        {
            RulebaseReport[] rulebases = new RulebaseReport[1];
            Rule[] copiedRules = new Rule[rules.Length];
            for (int index = 0; index < rules.Length; index++)
            {
                copiedRules[index] = rules[index];
            }

            rulebases[0] = new RulebaseReport
            {
                Id = 22,
                Rules = copiedRules
            };
            return rulebases;
        }

        private static Rule CreateRule(long id, bool recert, bool toBeRemoved = false)
        {
            return new Rule
            {
                Id = id,
                RulebaseId = 101,
                Uid = $"rule-{id}",
                Metadata = new RuleMetadata
                {
                    Recert = recert,
                    ToBeRemoved = toBeRemoved
                }
            };
        }

        private sealed class NoopCertificationApiConnection : SimulatedApiConnection
        {
            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                throw new InvalidOperationException($"Unexpected query: {query}");
            }

            public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler, GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null, string? operationName = null)
            {
                throw new InvalidOperationException("Unexpected subscription request");
            }
        }

        private sealed class FailingCertificationApiConnection : SimulatedApiConnection
        {
            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                throw new InvalidOperationException("Load failure");
            }

            public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler, GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null, string? operationName = null)
            {
                throw new InvalidOperationException("Unexpected subscription request");
            }
        }

        private sealed class RecordingCertificationApiConnection : SimulatedApiConnection
        {
            public List<(string Query, object? Variables)> Queries { get; } = [];
            public List<FwoOwner> Owners { get; set; } = [];
            public List<FwoOwner> EditableOwners { get; set; } = [];
            public List<ManagementSelect> DeviceManagements { get; set; } = [];
            public Dictionary<long, int> RecertifyRowsByRuleId { get; } = [];
            public Dictionary<long, List<Recertification>> OpenRecertsByRuleId { get; } = [];

            public int CountQuery(string query)
            {
                return Queries.Count(item => item.Query == query);
            }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                Queries.Add((query, variables));

                if (query == OwnerQueries.getOwners && typeof(QueryResponseType) == typeof(List<FwoOwner>))
                {
                    return Task.FromResult((QueryResponseType)(object)Owners);
                }

                if (query == OwnerQueries.getEditableOwners && typeof(QueryResponseType) == typeof(List<FwoOwner>))
                {
                    return Task.FromResult((QueryResponseType)(object)EditableOwners);
                }

                if (query == DeviceQueries.getDevicesByManagement && typeof(QueryResponseType) == typeof(List<ManagementSelect>))
                {
                    return Task.FromResult((QueryResponseType)(object)DeviceManagements);
                }

                if (query == RecertQueries.recertify && typeof(QueryResponseType) == typeof(ReturnId))
                {
                    long ruleId = GetLongProperty(variables, "ruleId");
                    int affectedRows = RecertifyRowsByRuleId.TryGetValue(ruleId, out int rows) ? rows : 1;
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = affectedRows });
                }

                if (query == RecertQueries.prepareNextRecertification)
                {
                    return Task.FromResult(default(QueryResponseType)!);
                }

                if (query == RecertQueries.getOpenRecertsForRule && typeof(QueryResponseType) == typeof(List<Recertification>))
                {
                    long ruleId = GetLongProperty(variables, "ruleId");
                    if (OpenRecertsByRuleId.TryGetValue(ruleId, out List<Recertification>? recerts))
                    {
                        return Task.FromResult((QueryResponseType)(object)recerts);
                    }
                    return Task.FromResult((QueryResponseType)(object)new List<Recertification>());
                }

                throw new InvalidOperationException($"Unexpected query: {query}");
            }

            private static long GetLongProperty(object? variables, string propertyName)
            {
                PropertyInfo? property = variables?.GetType().GetProperty(propertyName);
                return property != null ? Convert.ToInt64(property.GetValue(variables)!) : 0;
            }
        }
    }
}
