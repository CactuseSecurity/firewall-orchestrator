using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.File;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Middleware.Client;
using FWO.Middleware.Server;
using FWO.Test.Mocks;
using FWO.Ui.Pages.Settings;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;

namespace FWO.Test
{
    [TestFixture]
    [NonParallelizable]
    internal class UiSettingsManagementsTest
    {
        private static readonly FieldInfo JwtPublicKeyField = typeof(ConfigFile).GetField("jwtPublicKey", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingFieldException(typeof(ConfigFile).FullName, "jwtPublicKey");

        private RsaSecurityKey? originalJwtPublicKey;

        [SetUp]
        public void SetUp()
        {
            originalJwtPublicKey = (RsaSecurityKey?)JwtPublicKeyField.GetValue(null);

            SimulatedUserConfig.DummyTranslate["managements"] = "Managements";
            SimulatedUserConfig.DummyTranslate["U5111"] = "Managements overview";
            SimulatedUserConfig.DummyTranslate["add_new_management"] = "Add management";
            SimulatedUserConfig.DummyTranslate["remove_sample_data"] = "Remove sample data";
            SimulatedUserConfig.DummyTranslate["actions"] = "Actions";
            SimulatedUserConfig.DummyTranslate["clone"] = "Clone";
            SimulatedUserConfig.DummyTranslate["edit"] = "Edit";
            SimulatedUserConfig.DummyTranslate["delete"] = "Delete";
            SimulatedUserConfig.DummyTranslate["autodiscover"] = "Autodiscover";
            SimulatedUserConfig.DummyTranslate["id"] = "Id";
            SimulatedUserConfig.DummyTranslate["name"] = "Name";
            SimulatedUserConfig.DummyTranslate["uid"] = "Uid";
            SimulatedUserConfig.DummyTranslate["type"] = "Type";
            SimulatedUserConfig.DummyTranslate["host"] = "Host";
            SimulatedUserConfig.DummyTranslate["readonly_credential_mgm"] = "Readonly credential";
            SimulatedUserConfig.DummyTranslate["config_path"] = "Config path";
            SimulatedUserConfig.DummyTranslate["super_manager"] = "Super manager";
            SimulatedUserConfig.DummyTranslate["importer_host"] = "Importer host";
            SimulatedUserConfig.DummyTranslate["import_enabled"] = "Import enabled";
            SimulatedUserConfig.DummyTranslate["debug_level"] = "Debug level";
            SimulatedUserConfig.DummyTranslate["edit_management"] = "Edit management";
            SimulatedUserConfig.DummyTranslate["save"] = "Save";
            SimulatedUserConfig.DummyTranslate["cancel"] = "Cancel";
            SimulatedUserConfig.DummyTranslate["fetch_managements"] = "Fetch managements";
            SimulatedUserConfig.DummyTranslate["save_management"] = "Save management";
            SimulatedUserConfig.DummyTranslate["delete_management"] = "Delete management";
            SimulatedUserConfig.DummyTranslate["manual_autodiscovery"] = "Manual autodiscovery";
            SimulatedUserConfig.DummyTranslate["changes_found"] = " changes found";
            SimulatedUserConfig.DummyTranslate["found_no_changes"] = "No changes found";
            SimulatedUserConfig.DummyTranslate["ran_into_exception"] = "Ran into exception: ";
            SimulatedUserConfig.DummyTranslate["U5101"] = "Delete management ";
            SimulatedUserConfig.DummyTranslate["U5102"] = "Delete sample managements";
            SimulatedUserConfig.DummyTranslate["E5101"] = "Management has devices";
            SimulatedUserConfig.DummyTranslate["E5102"] = "Missing required management fields";
            SimulatedUserConfig.DummyTranslate["E5103"] = "Invalid management port";
            SimulatedUserConfig.DummyTranslate["E5104"] = "Invalid debug level";
            SimulatedUserConfig.DummyTranslate["E5105"] = "Duplicate management";
            SimulatedUserConfig.DummyTranslate["U0001"] = "Sanitized input";
        }

        [TearDown]
        public void TearDown()
        {
            JwtPublicKeyField.SetValue(null, originalJwtPublicKey);
        }

        [Test]
        public async Task OnInitializedAsync_LoadsManagementsDeviceTypesAndCredentialsAndShowsCleanupButton()
        {
            RecordingManagementsApiConnection apiConnection = new()
            {
                Managements =
                [
                    new Management
                    {
                        Id = 1,
                        Name = $"mgmt{GlobalConst.k_demo}",
                        Hostname = "sample.example.org",
                        Port = 443,
                        DeviceType = new DeviceType { Id = 12, Name = "FortiManager", Version = "7", Manufacturer = "Fortinet", IsManagement = true },
                        ImportCredential = new ImportCredential { Id = 1, Name = "Sample cred" }
                    },
                    new Management
                    {
                        Id = 2,
                        Name = "regular-mgmt",
                        Hostname = "regular.example.org",
                        Port = 8443,
                        DeviceType = new DeviceType { Id = 10, Name = "FortiGate", Version = "7", Manufacturer = "Fortinet", IsManagement = true },
                        ImportCredential = new ImportCredential { Id = 1, Name = "Sample cred" }
                    }
                ],
                DeviceTypes =
                [
                    new DeviceType { Id = 10, Name = "FortiGate", Version = "7", Manufacturer = "Fortinet", IsManagement = true },
                    new DeviceType { Id = 12, Name = "FortiManager", Version = "7", Manufacturer = "Fortinet", IsManagement = true }
                ],
                Credentials =
                [
                    new ImportCredential { Id = 1, Name = "Sample cred" }
                ]
            };

            SettingsManagements component = CreateDirectComponent(apiConnection, await CreateAuthorizedTokenService());

            await InvokePrivateTask(component, "OnInitializedAsync");

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.Queries, Is.EqualTo(new[]
                {
                    DeviceQueries.getManagementsDetails,
                    DeviceQueries.getDeviceTypeDetails,
                    DeviceQueries.getCredentials
                }));
                Assert.That(GetMember<List<Management>>(component, "managements"), Has.Count.EqualTo(2));
                Assert.That(GetMember<List<DeviceType>>(component, "deviceTypes"), Has.Count.EqualTo(2));
                Assert.That(GetMember<List<ImportCredential>>(component, "credentials"), Has.Count.EqualTo(1));
                Assert.That(GetMember<List<Management>>(component, "sampleManagements"), Has.Count.EqualTo(1));
                Assert.That(GetMember<bool>(component, "showCleanupButton"), Is.True);
            });
        }

        [Test]
        public async Task OnInitializedAsync_ShowsMessageWhenManagementLoadFails()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            RecordingManagementsApiConnection apiConnection = new()
            {
                ThrowOnManagementsQuery = true
            };

            SettingsManagements component = CreateDirectComponent(apiConnection, await CreateAuthorizedTokenService(), messages);

            await InvokePrivateTask(component, "OnInitializedAsync");

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("Fetch managements"));
                Assert.That(messages[0].Message, Is.Empty);
                Assert.That(messages[0].Exception, Is.TypeOf<InvalidOperationException>());
                Assert.That(messages[0].IsError, Is.True);
                Assert.That(apiConnection.Queries, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public void RequestDelete_ShowsGuardWhenManagementHasDevices()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            SettingsManagements component = CreateDirectComponent(out _, messages);
            Management management = BuildManagement(1, "mgmt-with-devices", devices: [new Device()]);
            SetMember(component, "managements", new List<Management> { management });

            InvokePrivateVoid(component, "RequestDelete", management);

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("Delete management"));
                Assert.That(messages[0].Message, Is.EqualTo("Management has devices"));
                Assert.That(GetMember<bool>(component, "DeleteMode"), Is.False);
            });
        }

        [Test]
        public void RequestDelete_AllowsDeleteWhenManagementIsEmpty()
        {
            SettingsManagements component = CreateDirectComponent(out _);
            Management management = BuildManagement(2, "mgmt-empty");
            SetMember(component, "managements", new List<Management> { management });

            InvokePrivateVoid(component, "RequestDelete", management);

            Assert.Multiple(() =>
            {
                Assert.That(GetMember<bool>(component, "DeleteMode"), Is.True);
                Assert.That(GetMember<string>(component, "deleteMessage"), Does.Contain("mgmt-empty"));
            });
        }

        [Test]
        public async Task Save_AddMode_PersistsNewManagementAndClearsEditModes()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            RecordingManagementsApiConnection apiConnection = new()
            {
                Managements =
                [
                    BuildManagement(1, $"mgmt{GlobalConst.k_demo}"),
                    BuildManagement(2, "existing-mgmt")
                ],
                DeviceTypes =
                [
                    new DeviceType { Id = 10, Name = "FortiGate", Version = "7", Manufacturer = "Fortinet", IsManagement = true }
                ],
                Credentials =
                [
                    new ImportCredential { Id = 5, Name = "Read cred" }
                ],
                NewManagementResult = new ReturnIdWrapper
                {
                    ReturnIds = [new ReturnId { NewId = 77 }]
                }
            };

            await using BunitContext context = CreateContext();
            TokenService tokenService = await CreateAuthorizedTokenService();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new AllowAllAuthStateProvider(Roles.Admin));
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            context.Services.AddSingleton<GlobalConfig>(new SimulatedGlobalConfig());
            context.Services.AddSingleton(tokenService);
            context.Services.AddSingleton<DomEventService>();
            context.Services.AddSingleton(typeof(IStringLocalizer<>), typeof(EmptyStringLocalizer<>));

            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context, (exception, title, message, isError) =>
            {
                messages.Add((exception, title, message, isError));
            });
            SettingsManagements component = wrapper.FindComponent<SettingsManagements>().Instance;

            wrapper.WaitForAssertion(() => Assert.That(GetMember<List<Management>>(component, "managements"), Has.Count.EqualTo(2)));

            SetMember(component, "AddMode", true);
            SetMember(component, "EditMode", true);
            SetMember(component, "actManagement", new Management
            {
                Name = "new-management",
                Uid = "new-management",
                Hostname = "new-management.example.org",
                Port = 443,
                DeviceType = GetMember<List<DeviceType>>(component, "deviceTypes")[0],
                ImportCredential = GetMember<List<ImportCredential>>(component, "credentials")[0],
                ImporterHostname = "importer.example.org",
                DebugLevel = 3
            });

            await wrapper.InvokeAsync(async () => await InvokePrivateTask(component, "Save"));

            Assert.Multiple(() =>
            {
                Assert.That(messages, Is.Empty);
                Assert.That(apiConnection.Queries.Count(query => query == DeviceQueries.newManagement), Is.EqualTo(1));
                Assert.That(GetMember<bool>(component, "AddMode"), Is.False);
                Assert.That(GetMember<bool>(component, "EditMode"), Is.False);
                Assert.That(GetMember<List<Management>>(component, "managements"), Has.Count.EqualTo(3));
                Assert.That(GetMember<List<Management>>(component, "managements")[2].Id, Is.EqualTo(77));
            });
        }

        private static BunitContext CreateContext()
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            return context;
        }

        private static async Task<TokenService> CreateAuthorizedTokenService()
        {
            using RSA rsa = RSA.Create(2048);
            RsaSecurityKey privateKey = new(rsa.ExportParameters(true));
            RsaSecurityKey publicKey = new(rsa.ExportParameters(false));
            JwtPublicKeyField.SetValue(null, publicKey);

            JwtWriter jwtWriter = new(privateKey);
            TokenService tokenService = new(new MockMiddlewareClient(), new MockProtectedSessionStorage());
            string accessToken = jwtWriter.CreateJWT(new UiUser
            {
                Name = "settings-admin",
                DbId = 7,
                Dn = "uid=settings-admin,ou=people,dc=fworch,dc=internal",
                Roles = [Roles.Admin]
            }, TimeSpan.FromHours(1));

            await tokenService.SetTokenPair(new TokenPair
            {
                AccessToken = accessToken,
                RefreshToken = "",
                AccessTokenExpires = DateTime.UtcNow.AddHours(1),
                RefreshTokenExpires = DateTime.UtcNow.AddDays(1)
            });

            return tokenService;
        }

        private static SettingsManagements CreateDirectComponent(RecordingManagementsApiConnection apiConnection, TokenService tokenService, List<(Exception? Exception, string Title, string Message, bool IsError)>? messages = null)
        {
            SettingsManagements component = new();
            SetMember(component, "apiConnection", apiConnection);
            SetMember(component, "userConfig", new SimulatedUserConfig
            {
                User =
                {
                    DbId = 7,
                    Roles = [Roles.Admin]
                }
            });
            SetMember(component, "tokenService", tokenService);
            SetMember(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((exception, title, message, isError) =>
            {
                messages?.Add((exception, title, message, isError));
            }));
            return component;
        }

        private static SettingsManagements CreateDirectComponent(out RecordingManagementsApiConnection apiConnection, List<(Exception? Exception, string Title, string Message, bool IsError)>? messages = null)
        {
            apiConnection = new RecordingManagementsApiConnection();
            return CreateDirectComponent(apiConnection, new TokenService(new MockMiddlewareClient(), new MockProtectedSessionStorage()), messages);
        }

        private static IRenderedComponent<CascadingAuthenticationState> RenderComponent(BunitContext context, Action<Exception?, string, string, bool>? displayMessageInUi = null)
        {
            Action<Exception?, string, string, bool> callback = displayMessageInUi ?? ((_, _, _, _) => { });
            return context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<CascadingValue<Action<Exception?, string, string, bool>>>(child => child
                    .Add(p => p.Value, callback)
                    .AddChildContent<SettingsManagements>()));
        }

        private static Management BuildManagement(int id, string name, Device[]? devices = null)
        {
            return new Management
            {
                Id = id,
                Name = name,
                Hostname = $"{name}.example.org",
                Port = 443,
                DeviceType = new DeviceType { Id = 10, Name = "FortiGate", Version = "7", Manufacturer = "Fortinet", IsManagement = true },
                ImportCredential = new ImportCredential { Id = 5, Name = "Read cred" },
                Devices = devices ?? []
            };
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

        private static async Task InvokePrivateTask(object instance, string methodName, params object?[] args)
        {
            MethodInfo method = FindPrivateMethod(instance.GetType(), methodName, args);
            Task task = (Task)(method.Invoke(instance, args) ?? throw new InvalidOperationException($"{methodName} returned null task."));
            await task;
        }

        private static void InvokePrivateVoid(object instance, string methodName, params object?[] args)
        {
            MethodInfo method = FindPrivateMethod(instance.GetType(), methodName, args);
            method.Invoke(instance, args);
        }

        private static MethodInfo FindPrivateMethod(Type type, string methodName, object?[] args)
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                         .Where(m => m.Name == methodName))
            {
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != args.Length)
                {
                    continue;
                }

                bool matches = true;
                for (int i = 0; i < parameters.Length; i++)
                {
                    object? arg = args[i];
                    Type parameterType = parameters[i].ParameterType;
                    if (arg == null)
                    {
                        if (parameterType.IsValueType && Nullable.GetUnderlyingType(parameterType) == null)
                        {
                            matches = false;
                            break;
                        }
                    }
                    else if (!parameterType.IsAssignableFrom(arg.GetType()))
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                {
                    return method;
                }
            }

            throw new MissingMethodException(type.FullName, methodName);
        }

        private sealed class EmptyStringLocalizer<T> : IStringLocalizer<T>
        {
            public LocalizedString this[string name] => new(name, name, resourceNotFound: true);

            public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments), resourceNotFound: true);

            public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];

            public EmptyStringLocalizer<T> WithCulture(System.Globalization.CultureInfo culture) => this;
        }

        private sealed class RecordingManagementsApiConnection : SimulatedApiConnection
        {
            public List<string> Queries { get; } = [];
            public List<object?> Variables { get; } = [];
            public List<Management> Managements { get; set; } = [];
            public List<DeviceType> DeviceTypes { get; set; } = [];
            public List<ImportCredential> Credentials { get; set; } = [];
            public ReturnIdWrapper NewManagementResult { get; set; } = new();
            public ReturnId UpdateManagementResult { get; set; } = new();
            public ReturnId DeleteManagementResult { get; set; } = new();
            public bool ThrowOnManagementsQuery { get; set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                Queries.Add(query);
                Variables.Add(variables);

                if (ThrowOnManagementsQuery && query == DeviceQueries.getManagementsDetails)
                {
                    throw new InvalidOperationException("management load failed");
                }

                if ((query == DeviceQueries.getManagementsDetails || query == DeviceQueries.getManagementDetailsWithoutSecrets) &&
                    typeof(QueryResponseType) == typeof(List<Management>))
                {
                    return Task.FromResult((QueryResponseType)(object)Managements.Select(item => new Management(item)).ToList());
                }

                if (query == DeviceQueries.getDeviceTypeDetails && typeof(QueryResponseType) == typeof(List<DeviceType>))
                {
                    return Task.FromResult((QueryResponseType)(object)DeviceTypes.Select(item => new DeviceType(item)).ToList());
                }

                if ((query == DeviceQueries.getCredentials || query == DeviceQueries.getCredentialsWithoutSecrets) &&
                    typeof(QueryResponseType) == typeof(List<ImportCredential>))
                {
                    return Task.FromResult((QueryResponseType)(object)Credentials.Select(item => new ImportCredential(item)).ToList());
                }

                if (query == DeviceQueries.newManagement && typeof(QueryResponseType) == typeof(ReturnIdWrapper))
                {
                    return Task.FromResult((QueryResponseType)(object)NewManagementResult);
                }

                if (query == DeviceQueries.updateManagement && typeof(QueryResponseType) == typeof(ReturnId))
                {
                    return Task.FromResult((QueryResponseType)(object)UpdateManagementResult);
                }

                if (query == DeviceQueries.deleteManagement && typeof(QueryResponseType) == typeof(ReturnId))
                {
                    return Task.FromResult((QueryResponseType)(object)DeleteManagementResult);
                }

                if (query == MonitorQueries.addAutodiscoveryLogEntry && typeof(QueryResponseType) == typeof(ReturnIdWrapper))
                {
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper { ReturnIds = [] });
                }

                throw new InvalidOperationException($"Unexpected query: {query}");
            }
        }
    }
}
