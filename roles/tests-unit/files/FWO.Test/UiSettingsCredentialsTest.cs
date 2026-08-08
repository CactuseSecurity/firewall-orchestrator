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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Security.Authentication;
using System.Security.Cryptography;

namespace FWO.Test
{
    [TestFixture]
    [NonParallelizable]
    internal class UiSettingsCredentialsTest
    {
        private static readonly FieldInfo JwtPublicKeyField = typeof(ConfigFile).GetField("jwtPublicKey", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingFieldException(typeof(ConfigFile).FullName, "jwtPublicKey");

        private RsaSecurityKey? originalJwtPublicKey;
        private string? mainKeyFilePath;
        private bool mainKeyFileAvailable;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            originalJwtPublicKey = (RsaSecurityKey?)JwtPublicKeyField.GetValue(null);

            mainKeyFilePath = GlobalConst.kMainKeyFile;
            try
            {
                string? keyDirectory = Path.GetDirectoryName(mainKeyFilePath);
                if (!string.IsNullOrWhiteSpace(keyDirectory))
                {
                    Directory.CreateDirectory(keyDirectory);
                }

                File.WriteAllText(mainKeyFilePath, "0123456789ABCDEF0123456789ABCDEF");
                mainKeyFileAvailable = true;
            }
            catch (UnauthorizedAccessException)
            {
                mainKeyFileAvailable = false;
            }
            catch (IOException)
            {
                mainKeyFileAvailable = false;
            }
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            JwtPublicKeyField.SetValue(null, originalJwtPublicKey);

            if (!string.IsNullOrWhiteSpace(mainKeyFilePath) && File.Exists(mainKeyFilePath))
            {
                File.Delete(mainKeyFilePath);
            }
        }

        [SetUp]
        public void SetUp()
        {
            SimulatedUserConfig.DummyTranslate["readonly_credential"] = "Credentials";
            SimulatedUserConfig.DummyTranslate["U5116"] = "Credential settings";
            SimulatedUserConfig.DummyTranslate["add_new_credential"] = "Add credential";
            SimulatedUserConfig.DummyTranslate["remove_sample_data"] = "Remove sample data";
            SimulatedUserConfig.DummyTranslate["actions"] = "Actions";
            SimulatedUserConfig.DummyTranslate["clone"] = "Clone";
            SimulatedUserConfig.DummyTranslate["edit"] = "Edit";
            SimulatedUserConfig.DummyTranslate["delete"] = "Delete";
            SimulatedUserConfig.DummyTranslate["action"] = "Action";
            SimulatedUserConfig.DummyTranslate["id"] = "Id";
            SimulatedUserConfig.DummyTranslate["name"] = "Name";
            SimulatedUserConfig.DummyTranslate["username"] = "Username";
            SimulatedUserConfig.DummyTranslate["is_key_pair"] = "Key pair";
            SimulatedUserConfig.DummyTranslate["edit_credential"] = "Edit credential";
            SimulatedUserConfig.DummyTranslate["private_key"] = "Private key";
            SimulatedUserConfig.DummyTranslate["public_key"] = "Public key";
            SimulatedUserConfig.DummyTranslate["login_secret"] = "Login secret";
            SimulatedUserConfig.DummyTranslate["cloud_client_id"] = "Cloud client id";
            SimulatedUserConfig.DummyTranslate["cloud_client_secret"] = "Cloud client secret";
            SimulatedUserConfig.DummyTranslate["save"] = "Save";
            SimulatedUserConfig.DummyTranslate["cancel"] = "Cancel";
            SimulatedUserConfig.DummyTranslate["fetch_credentials"] = "Fetch credentials";
            SimulatedUserConfig.DummyTranslate["save_credential"] = "Save credential";
            SimulatedUserConfig.DummyTranslate["delete_credential"] = "Delete credential";
            SimulatedUserConfig.DummyTranslate["E5102"] = "Missing required credential fields";
            SimulatedUserConfig.DummyTranslate["E5117"] = "Credential is used by managements";
            SimulatedUserConfig.DummyTranslate["U5117"] = "Delete credential ";
            SimulatedUserConfig.DummyTranslate["U5108"] = "Remove sample credentials";
            SimulatedUserConfig.DummyTranslate["U0001"] = "Sanitized input";
        }

        [Test]
        public async Task OnInitializedAsync_LoadsCredentialsAndShowsCleanupButtonForAdmin()
        {
            await using RenderSetup setup = await CreateRenderedSetup(CreateApiConnection(), Roles.Admin);
            RecordingCredentialsApiConnection apiConnection = setup.ApiConnection;
            SettingsCredentials component = setup.Component;

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.Queries, Has.Count.EqualTo(1));
                Assert.That(apiConnection.Queries[0], Is.EqualTo(DeviceQueries.getCredentials));
                Assert.That(GetMember<List<ImportCredential>>(component, "credentials"), Has.Count.EqualTo(3));
                Assert.That(GetMember<List<ImportCredential>>(component, "sampleCredentials"), Has.Count.EqualTo(2));
                Assert.That(GetMember<bool>(component, "showCleanupButton"), Is.True);
            });
        }

        [Test]
        public async Task Refresh_LoadsCredentialsWithoutSecretsForAuditor()
        {
            await using RenderSetup setup = await CreateRenderedSetup(CreateApiConnection(), Roles.Auditor);
            RecordingCredentialsApiConnection apiConnection = setup.ApiConnection;
            SettingsCredentials component = setup.Component;

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.Queries, Has.Count.EqualTo(1));
                Assert.That(apiConnection.Queries[0], Is.EqualTo(DeviceQueries.getCredentialsWithoutSecrets));
                Assert.That(GetMember<List<ImportCredential>>(component, "credentials")[0].Secret, Is.Empty);
            });
        }

        [Test]
        public async Task Refresh_ShowsErrorWhenAccessTokenIsMissing()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            await using RenderSetup setup = await CreateRenderedSetup(CreateApiConnection(), Roles.Admin, messages, withToken: false);

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("Fetch credentials"));
                Assert.That(messages[0].IsError, Is.True);
                Assert.That(messages[0].Exception, Is.TypeOf<AuthenticationException>());
            });
        }

        [Test]
        public async Task RequestDelete_ShowsGuardWhenCredentialIsUsed()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            await using RenderSetup setup = await CreateRenderedSetup(CreateApiConnection(), Roles.Admin, messages);
            RecordingCredentialsApiConnection apiConnection = setup.ApiConnection;
            SettingsCredentials component = setup.Component;
            ImportCredential credential = GetMember<List<ImportCredential>>(component, "credentials")[0];
            apiConnection.MgmtCountUsingCred = 2;

            await setup.Rendered.InvokeAsync(() => InvokePrivateTask(component, "RequestDelete", credential));

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("Delete credential"));
                Assert.That(messages[0].Message, Is.EqualTo("Credential is used by managements"));
                Assert.That(GetMember<bool>(component, "DeleteMode"), Is.False);
            });
        }

        [Test]
        public async Task RequestDelete_AllowsDeleteWhenCredentialIsUnused()
        {
            await using RenderSetup setup = await CreateRenderedSetup(CreateApiConnection(), Roles.Admin);
            RecordingCredentialsApiConnection apiConnection = setup.ApiConnection;
            SettingsCredentials component = setup.Component;
            ImportCredential credential = GetMember<List<ImportCredential>>(component, "credentials")[0];
            apiConnection.MgmtCountUsingCred = 0;

            await setup.Rendered.InvokeAsync(() => InvokePrivateTask(component, "RequestDelete", credential));

            Assert.Multiple(() =>
            {
                Assert.That(GetMember<bool>(component, "DeleteMode"), Is.True);
                Assert.That(GetMember<string>(component, "deleteMessage"), Does.Contain(credential.Name));
            });
        }

        [Test]
        public async Task Delete_RemovesCredentialWhenConfirmed()
        {
            await using RenderSetup setup = await CreateRenderedSetup(CreateApiConnection(), Roles.Admin);
            RecordingCredentialsApiConnection apiConnection = setup.ApiConnection;
            apiConnection.DeleteCredentialResult = new ReturnId { DeletedId = 1 };
            SettingsCredentials component = setup.Component;
            ImportCredential credential = GetMember<List<ImportCredential>>(component, "credentials")[0];
            SetMember(component, "actCredential", credential);
            SetMember(component, "DeleteMode", true);

            await setup.Rendered.InvokeAsync(() => InvokePrivateTask(component, "Delete"));

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.Queries.Count(query => query == DeviceQueries.deleteCredential), Is.EqualTo(1));
                Assert.That(GetMember<List<ImportCredential>>(component, "credentials"), Has.Count.EqualTo(2));
                Assert.That(GetMember<bool>(component, "DeleteMode"), Is.False);
                Assert.That(GetMember<bool>(component, "workInProgress"), Is.False);
            });
        }

        [Test]
        public async Task RemoveSampleData_RemovesDemoCredentials()
        {
            await using RenderSetup setup = await CreateRenderedSetup(CreateApiConnection(), Roles.Admin);
            RecordingCredentialsApiConnection apiConnection = setup.ApiConnection;
            SettingsCredentials component = setup.Component;

            InvokePrivateVoid(component, "RequestRemoveSampleData");
            await setup.Rendered.InvokeAsync(() => InvokePrivateTask(component, "RemoveSampleData"));

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.Queries.Count(query => query == DeviceQueries.deleteCredential), Is.EqualTo(2));
                Assert.That(GetMember<List<ImportCredential>>(component, "credentials"), Has.Count.EqualTo(1));
                Assert.That(GetMember<bool>(component, "CleanupMode"), Is.False);
                Assert.That(GetMember<bool>(component, "showCleanupButton"), Is.False);
            });
        }

        [Test]
        public async Task Save_AddMode_PersistsNewCredentialAndEncryptsSecret()
        {
            Assume.That(mainKeyFileAvailable, "Requires a writable main key file path for password encryption.");

            await using RenderSetup setup = await CreateRenderedSetup(CreateApiConnection(), Roles.Admin);
            RecordingCredentialsApiConnection apiConnection = setup.ApiConnection;
            apiConnection.NewCredentialResult = new ReturnIdWrapper
            {
                ReturnIds = [new ReturnId { NewId = 77 }]
            };
            SettingsCredentials component = setup.Component;
            InvokePrivateVoid(component, "Add");
            SetMember(component, "actCredential", new ImportCredential
            {
                Name = "new-credential",
                ImportUser = "import-user",
                Secret = "plain-secret",
                IsKeyPair = false,
                CloudClientId = "client-id",
                CloudClientSecret = "client-secret"
            });

            await component.Save();

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.Queries.Count(query => query == DeviceQueries.newCredential), Is.EqualTo(1));
                Assert.That(GetMember<List<ImportCredential>>(component, "credentials"), Has.Count.EqualTo(4));
                Assert.That(GetMember<bool>(component, "AddMode"), Is.False);
                Assert.That(GetMember<bool>(component, "EditMode"), Is.False);

                Dictionary<string, object?> variables = apiConnection.LastNewCredentialVariables;
                Assert.That(variables["credential_name"], Is.EqualTo("new-credential"));
                Assert.That(variables["username"], Is.EqualTo("import-user"));
                Assert.That((string)variables["secret"]!, Is.Not.EqualTo("plain-secret"));
            });
        }

        [Test]
        public async Task Save_UpdatesExistingCredentialSuccessfully()
        {
            Assume.That(mainKeyFileAvailable, "Requires a writable main key file path for password encryption.");
            await using RenderSetup setup = await CreateRenderedSetup(CreateApiConnection(), Roles.Admin);
            RecordingCredentialsApiConnection apiConnection = setup.ApiConnection;
            apiConnection.UpdateCredentialResult = new ReturnId { UpdatedId = 1 };
            SettingsCredentials component = setup.Component;
            ImportCredential credential = GetMember<List<ImportCredential>>(component, "credentials")[0];
            InvokePrivateVoid(component, "Edit", credential);
            SetMember(component, "actCredential", new ImportCredential
            {
                Id = credential.Id,
                Name = "cred-one-renamed",
                ImportUser = "import-user",
                Secret = "plain-secret",
                IsKeyPair = false,
                CloudClientId = "client-id",
                CloudClientSecret = "client-secret"
            });

            await component.Save();

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.Queries.Count(query => query == DeviceQueries.updateCredential), Is.EqualTo(1));
                Assert.That(GetMember<List<ImportCredential>>(component, "credentials")[0].Name, Is.EqualTo("cred-one-renamed"));
                Assert.That(GetMember<bool>(component, "EditMode"), Is.False);
            });
        }

        [Test]
        public async Task Save_ShowsValidationWhenRequiredFieldsMissing()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            await using RenderSetup setup = await CreateRenderedSetup(CreateApiConnection(), Roles.Admin, messages);
            RecordingCredentialsApiConnection apiConnection = setup.ApiConnection;
            SettingsCredentials component = setup.Component;
            InvokePrivateVoid(component, "Add");
            SetMember(component, "actCredential", new ImportCredential
            {
                Name = "",
                ImportUser = "import-user",
                Secret = "",
                IsKeyPair = false
            });

            await component.Save();

            Assert.Multiple(() =>
            {
                Assert.That(messages.Any(message =>
                    message.Title == "Save credential" &&
                    message.Message == "Missing required credential fields" &&
                    message.IsError), Is.True);
                Assert.That(apiConnection.Queries.Any(query => query == DeviceQueries.newCredential || query == DeviceQueries.updateCredential), Is.False);
            });
        }

        private static RecordingCredentialsApiConnection CreateApiConnection()
        {
            return new RecordingCredentialsApiConnection
            {
                Credentials =
                [
                    new ImportCredential
                    {
                        Id = 1,
                        Name = "cred-one_demo",
                        ImportUser = "import-one",
                        Secret = "secret-one",
                        IsKeyPair = false
                    },
                    new ImportCredential
                    {
                        Id = 2,
                        Name = "cred-two_demo",
                        ImportUser = "import-two",
                        Secret = "secret-two",
                        IsKeyPair = true,
                        PublicKey = "public-two"
                    },
                    new ImportCredential
                    {
                        Id = 3,
                        Name = "cred-three",
                        ImportUser = "import-three",
                        Secret = "secret-three",
                        IsKeyPair = false
                    }
                ]
            };
        }

        private static async Task<RenderSetup> CreateRenderedSetup(
            RecordingCredentialsApiConnection apiConnection,
            string role,
            List<(Exception? Exception, string Title, string Message, bool IsError)>? messages = null,
            bool withToken = true)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new AllowAllAuthStateProvider(role));
            context.Services.AddSingleton<DomEventService>();
            context.Services.AddSingleton(typeof(IStringLocalizer<>), typeof(EmptyStringLocalizer<>));
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddSingleton<UserConfig>(CreateUserConfig(role));
            context.Services.AddSingleton<MiddlewareClient>(new MockMiddlewareClient());

            TokenService tokenService = new(new MockMiddlewareClient(), new MockProtectedSessionStorage());
            if (withToken)
            {
                await tokenService.SetTokenPair(CreateTokenPair(role));
            }
            context.Services.AddSingleton(tokenService);

            IRenderedComponent<CascadingAuthenticationState> rendered = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<CascadingValue<Action<Exception?, string, string, bool>>>(child => child
                    .Add(p => p.Value, (exception, title, message, isError) =>
                    {
                        messages?.Add((exception, title, message, isError));
                    })
                    .AddChildContent<SettingsCredentials>()));

            await rendered.InvokeAsync(() => Task.CompletedTask);
            if (withToken)
            {
                rendered.WaitForAssertion(() =>
                {
                    SettingsCredentials component = rendered.FindComponent<SettingsCredentials>().Instance;
                    Assert.That(GetMember<List<ImportCredential>>(component, "credentials"), Has.Count.EqualTo(apiConnection.Credentials.Count));
                });
            }
            return new RenderSetup(context, rendered, rendered.FindComponent<SettingsCredentials>().Instance, apiConnection);
        }

        private static SimulatedUserConfig CreateUserConfig(string role)
        {
            SimulatedUserConfig userConfig = new();
            userConfig.User.Roles = [role];
            return userConfig;
        }

        private static TokenPair CreateTokenPair(string role)
        {
            using RSA rsa = RSA.Create(2048);
            RsaSecurityKey privateKey = new(rsa.ExportParameters(true));
            RsaSecurityKey publicKey = new(rsa.ExportParameters(false));
            JwtPublicKeyField.SetValue(null, publicKey);

            JwtWriter jwtWriter = new(privateKey);
            string accessToken = jwtWriter.CreateJWT(new UiUser
            {
                Name = "settings-credentials",
                DbId = 7,
                Dn = "uid=settings-credentials,ou=people,dc=fworch,dc=internal",
                Roles = [role]
            }, TimeSpan.FromHours(1));

            return new TokenPair
            {
                AccessToken = accessToken,
                RefreshToken = "",
                AccessTokenExpires = DateTime.UtcNow.AddHours(1),
                RefreshTokenExpires = DateTime.UtcNow.AddDays(1)
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

        private static void InvokePrivateVoid(object instance, string methodName, params object?[] args)
        {
            MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
            _ = method.Invoke(instance, args);
        }

        private static async Task InvokePrivateTask(object instance, string methodName, params object?[] args)
        {
            MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
            Task task = (Task)(method.Invoke(instance, args) ?? throw new InvalidOperationException($"{methodName} returned null task."));
            await task;
        }

        private sealed class RecordingCredentialsApiConnection : SimulatedApiConnection
        {
            public List<string> Queries { get; } = [];
            public List<ImportCredential> Credentials { get; set; } = [];
            public int MgmtCountUsingCred { get; set; }
            public ReturnIdWrapper NewCredentialResult { get; set; } = new() { ReturnIds = [new ReturnId { NewId = 10 }] };
            public ReturnId UpdateCredentialResult { get; set; } = new() { UpdatedId = 1 };
            public ReturnId DeleteCredentialResult { get; set; } = new() { DeletedId = 1 };
            public Dictionary<string, object?> LastNewCredentialVariables { get; private set; } = [];
            public Dictionary<string, object?> LastUpdateCredentialVariables { get; private set; } = [];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                Queries.Add(query);

                if (query == DeviceQueries.getCredentials && typeof(QueryResponseType) == typeof(List<ImportCredential>))
                {
                    return Task.FromResult((QueryResponseType)(object)Credentials.Select(item => new ImportCredential(item)).ToList());
                }

                if (query == DeviceQueries.getCredentialsWithoutSecrets && typeof(QueryResponseType) == typeof(List<ImportCredential>))
                {
                    List<ImportCredential> sanitized = Credentials.Select(item => new ImportCredential(item) { Secret = "" }).ToList();
                    return Task.FromResult((QueryResponseType)(object)sanitized);
                }

                if (query == DeviceQueries.getMgmtNumberUsingCred && typeof(QueryResponseType) == typeof(AggregateCount))
                {
                    return Task.FromResult((QueryResponseType)(object)new AggregateCount { Aggregate = new Aggregate { Count = MgmtCountUsingCred } });
                }

                if (query == DeviceQueries.deleteCredential && typeof(QueryResponseType) == typeof(ReturnId))
                {
                    PropertyInfo? idProperty = variables?.GetType().GetProperty("id");
                    int id = idProperty == null ? 0 : Convert.ToInt32(idProperty.GetValue(variables));
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { DeletedId = id == 0 ? DeleteCredentialResult.DeletedId : id });
                }

                if (query == DeviceQueries.newCredential && typeof(QueryResponseType) == typeof(ReturnIdWrapper))
                {
                    LastNewCredentialVariables = ExtractVariables(variables);
                    return Task.FromResult((QueryResponseType)(object)NewCredentialResult);
                }

                if (query == DeviceQueries.updateCredential && typeof(QueryResponseType) == typeof(ReturnId))
                {
                    LastUpdateCredentialVariables = ExtractVariables(variables);
                    return Task.FromResult((QueryResponseType)(object)UpdateCredentialResult);
                }

                throw new InvalidOperationException($"Unexpected query: {query}");
            }

            private static Dictionary<string, object?> ExtractVariables(object? variables)
            {
                if (variables == null)
                {
                    return [];
                }

                return variables.GetType()
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .ToDictionary(property => property.Name, property => property.GetValue(variables));
            }
        }

        private sealed class RenderSetup : IAsyncDisposable
        {
            public RenderSetup(BunitContext context, IRenderedComponent<CascadingAuthenticationState> rendered, SettingsCredentials component, RecordingCredentialsApiConnection apiConnection)
            {
                Context = context;
                Rendered = rendered;
                Component = component;
                ApiConnection = apiConnection;
            }

            public BunitContext Context { get; }
            public IRenderedComponent<CascadingAuthenticationState> Rendered { get; }
            public SettingsCredentials Component { get; }
            public RecordingCredentialsApiConnection ApiConnection { get; }

            public ValueTask DisposeAsync()
            {
                return Context.DisposeAsync();
            }
        }

        private sealed class EmptyStringLocalizer<T> : IStringLocalizer<T>
        {
            public LocalizedString this[string name] => new(name, name, resourceNotFound: true);
            public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments), resourceNotFound: true);
            public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
            public EmptyStringLocalizer<T> WithCulture(System.Globalization.CultureInfo culture) => this;
        }
    }
}
