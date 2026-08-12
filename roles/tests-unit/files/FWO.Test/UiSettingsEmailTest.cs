using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Mail;
using FWO.Middleware.Client;
using FWO.Test.Mocks;
using FWO.Ui.Pages.Settings;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.IO;
using System.Linq;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    [NonParallelizable]
    internal class UiSettingsEmailTest
    {
        private bool mainKeyFileAvailable;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            try
            {
                _ = File.ReadAllText(GlobalConst.kMainKeyFile);
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
        }

        [SetUp]
        public void SetUp()
        {
            SimulatedUserConfig.DummyTranslate["email_settings"] = "Email settings";
            SimulatedUserConfig.DummyTranslate["U5319"] = "Email settings intro";
            SimulatedUserConfig.DummyTranslate["address"] = "Address";
            SimulatedUserConfig.DummyTranslate["port"] = "Port";
            SimulatedUserConfig.DummyTranslate["email_enc_method"] = "Encryption";
            SimulatedUserConfig.DummyTranslate["email_auth_user"] = "Auth user";
            SimulatedUserConfig.DummyTranslate["email_auth_pwd"] = "Auth password";
            SimulatedUserConfig.DummyTranslate["email_sender"] = "Sender";
            SimulatedUserConfig.DummyTranslate["use_dummy_email_address"] = "Use dummy email";
            SimulatedUserConfig.DummyTranslate["dummy_email_address"] = "Dummy email";
            SimulatedUserConfig.DummyTranslate["test_connection"] = "Test connection";
            SimulatedUserConfig.DummyTranslate["save"] = "Save";
            SimulatedUserConfig.DummyTranslate["read_config"] = "Read config";
            SimulatedUserConfig.DummyTranslate["E5301"] = "Failed to load email config";
            SimulatedUserConfig.DummyTranslate["change_default"] = "Change default";
            SimulatedUserConfig.DummyTranslate["U5301"] = "Email settings saved.";
            SimulatedUserConfig.DummyTranslate["save_email_conn"] = "Save email connection";
            SimulatedUserConfig.DummyTranslate["E5102"] = "Missing email server";
            SimulatedUserConfig.DummyTranslate["E5103"] = "Invalid email port";
            SimulatedUserConfig.DummyTranslate["E5108"] = "Invalid sender address";
            SimulatedUserConfig.DummyTranslate["test_email_connection"] = "Test email connection";
            SimulatedUserConfig.DummyTranslate["E8101"] = "No user email configured";
            SimulatedUserConfig.DummyTranslate["U5402"] = "Email connection OK";
        }

        [Test]
        public async Task SettingsEmail_RendersEnabledButtonsForAdmin()
        {
            RecordingSettingsApiConn apiConnection = new();
            SimulatedGlobalConfig globalConfig = CreateGlobalConfig();
            SimulatedUserConfig userConfig = CreateUserConfig(Roles.Admin);

            await using BunitContext context = CreateContext(Roles.Admin, apiConnection, globalConfig, userConfig);
            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context);

            wrapper.WaitForAssertion(() =>
            {
                var buttons = wrapper.FindAll("button");
                Assert.That(buttons, Has.Count.EqualTo(2));
                Assert.That(buttons[0].HasAttribute("disabled"), Is.False);
                Assert.That(buttons[1].HasAttribute("disabled"), Is.False);
            });
        }

        [Test]
        public async Task SettingsEmail_RendersDisabledButtonsForAuditor()
        {
            RecordingSettingsApiConn apiConnection = new();
            SimulatedGlobalConfig globalConfig = CreateGlobalConfig();
            SimulatedUserConfig userConfig = CreateUserConfig(Roles.Auditor);

            await using BunitContext context = CreateContext(Roles.Auditor, apiConnection, globalConfig, userConfig);
            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context);

            wrapper.WaitForAssertion(() =>
            {
                var buttons = wrapper.FindAll("button");
                Assert.That(buttons, Has.Count.EqualTo(2));
                Assert.That(buttons[0].HasAttribute("disabled"), Is.True);
                Assert.That(buttons[1].HasAttribute("disabled"), Is.True);
            });
        }

        [Test]
        public async Task OnInitializedAsync_LoadsExistingEmailConnection()
        {
            RecordingSettingsApiConn apiConnection = new();
            SimulatedGlobalConfig globalConfig = CreateGlobalConfig();
            globalConfig.EmailServerAddress = "smtp.example.test";
            globalConfig.EmailPort = 587;
            globalConfig.EmailTls = EmailEncryptionMethod.StartTls;
            globalConfig.EmailUser = "smtp-user";
            globalConfig.EmailPassword = "stored-password";
            globalConfig.EmailSenderAddress = "noreply@example.test";
            SimulatedUserConfig userConfig = CreateUserConfig();
            SettingsEmail component = CreateComponent(apiConnection, globalConfig, userConfig);

            await InvokePrivateTask(component, "OnInitializedAsync");

            EmailConnection actEmailConnection = GetMember<EmailConnection>(component, "actEmailConnection");
            Assert.Multiple(() =>
            {
                Assert.That(actEmailConnection.ServerAddress, Is.EqualTo("smtp.example.test"));
                Assert.That(actEmailConnection.Port, Is.EqualTo(587));
                Assert.That(actEmailConnection.Encryption, Is.EqualTo(EmailEncryptionMethod.StartTls));
                Assert.That(actEmailConnection.User, Is.EqualTo("smtp-user"));
                Assert.That(actEmailConnection.Password, Is.EqualTo("stored-password"));
                Assert.That(actEmailConnection.SenderEmailAddress, Is.EqualTo("noreply@example.test"));
            });
        }

        [Test]
        public async Task OnInitializedAsync_ShowsErrorWhenConfigAccessFails()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            RecordingSettingsApiConn apiConnection = new();
            SimulatedGlobalConfig globalConfig = CreateGlobalConfig();
            globalConfig.Dispose();
            SimulatedUserConfig userConfig = CreateUserConfig();
            SettingsEmail component = CreateComponent(apiConnection, globalConfig, userConfig, messages);

            await InvokePrivateTask(component, "OnInitializedAsync");

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("Read config"));
                Assert.That(messages[0].Message, Is.EqualTo("Failed to load email config"));
                Assert.That(messages[0].Exception, Is.TypeOf<ObjectDisposedException>());
                Assert.That(messages[0].IsError, Is.False);
            });
        }

        [Test]
        public async Task Save_ShowsValidationWhenServerAddressIsMissing()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            (SettingsEmail component, RecordingSettingsApiConn apiConnection) = CreateInitializedComponent(messages);

            SetMember(component, "actEmailConnection", new EmailConnection
            {
                ServerAddress = "",
                Port = 587,
                Encryption = EmailEncryptionMethod.StartTls,
                SenderEmailAddress = "sender@example.test"
            });

            await InvokePrivateTask(component, "Save");

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.Queries, Is.Empty);
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("Save email connection"));
                Assert.That(messages[0].Message, Is.EqualTo("Missing email server"));
                Assert.That(messages[0].IsError, Is.True);
            });
        }

        [Test]
        public async Task Save_ShowsValidationWhenPortIsInvalid()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            (SettingsEmail component, RecordingSettingsApiConn apiConnection) = CreateInitializedComponent(messages);

            SetMember(component, "actEmailConnection", new EmailConnection
            {
                ServerAddress = "smtp.example.test",
                Port = 0,
                Encryption = EmailEncryptionMethod.StartTls,
                SenderEmailAddress = "sender@example.test"
            });

            await InvokePrivateTask(component, "Save");

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.Queries, Is.Empty);
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("Save email connection"));
                Assert.That(messages[0].Message, Is.EqualTo("Invalid email port"));
                Assert.That(messages[0].IsError, Is.True);
            });
        }

        [Test]
        public async Task Save_ShowsValidationWhenSenderAddressIsInvalid()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            (SettingsEmail component, RecordingSettingsApiConn apiConnection) = CreateInitializedComponent(messages);

            SetMember(component, "actEmailConnection", new EmailConnection
            {
                ServerAddress = "smtp.example.test",
                Port = 587,
                Encryption = EmailEncryptionMethod.StartTls,
                SenderEmailAddress = "not-an-email"
            });

            await InvokePrivateTask(component, "Save");

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.Queries, Is.Empty);
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("Save email connection"));
                Assert.That(messages[0].Message, Is.EqualTo("Invalid sender address"));
                Assert.That(messages[0].IsError, Is.True);
            });
        }

        [Test]
        public async Task Save_PersistsUpdatedEmailConfigAndEncryptsPassword()
        {
            Assume.That(mainKeyFileAvailable, "Requires a writable main key file path for password encryption.");

            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            RecordingSettingsApiConn apiConnection = new();
            SimulatedGlobalConfig globalConfig = CreateGlobalConfig();
            SimulatedUserConfig userConfig = CreateUserConfig();
            SettingsEmail component = CreateComponent(apiConnection, globalConfig, userConfig, messages);

            await InvokePrivateTask(component, "OnInitializedAsync");

            SetMember(component, "actEmailConnection", new EmailConnection
            {
                ServerAddress = "smtp.example.test",
                Port = 587,
                Encryption = EmailEncryptionMethod.StartTls,
                User = "smtp-user",
                Password = "smtp-password",
                SenderEmailAddress = "sender@example.test"
            });

            ConfigData editableConfig = GetMember<ConfigData>(component, "editableConfig");
            editableConfig.UseDummyEmailAddress = true;
            editableConfig.DummyEmailAddress = "dummy@example.test";

            await InvokePrivateTask(component, "Save");

            Dictionary<string, string> updatedItems = apiConnection.LastUpsertConfigItems.ToDictionary(item => item.Key, item => item.Value ?? "");
            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("Change default"));
                Assert.That(messages[0].Message, Is.EqualTo("Email settings saved."));
                Assert.That(messages[0].IsError, Is.False);
                Assert.That(apiConnection.Queries, Does.Contain(ConfigQueries.upsertConfigItems));
                Assert.That(globalConfig.EmailServerAddress, Is.EqualTo("smtp.example.test"));
                Assert.That(globalConfig.EmailPort, Is.EqualTo(587));
                Assert.That(globalConfig.EmailTls, Is.EqualTo(EmailEncryptionMethod.StartTls));
                Assert.That(globalConfig.EmailUser, Is.EqualTo("smtp-user"));
                Assert.That(globalConfig.EmailSenderAddress, Is.EqualTo("sender@example.test"));
                Assert.That(globalConfig.UseDummyEmailAddress, Is.True);
                Assert.That(globalConfig.DummyEmailAddress, Is.EqualTo("dummy@example.test"));
                Assert.That(updatedItems["emailServerAddress"], Is.EqualTo("smtp.example.test"));
                Assert.That(updatedItems["emailPort"], Is.EqualTo("587"));
                Assert.That(updatedItems["emailTls"], Is.EqualTo(nameof(EmailEncryptionMethod.StartTls)));
                Assert.That(updatedItems["emailUser"], Is.EqualTo("smtp-user"));
                Assert.That(updatedItems["emailPassword"], Is.Not.Empty);
                Assert.That(updatedItems["emailPassword"], Is.Not.EqualTo("smtp-password"));
                Assert.That(updatedItems["emailSenderAddress"], Is.EqualTo("sender@example.test"));
                Assert.That(updatedItems["useDummyEmailAddress"], Is.EqualTo("True"));
                Assert.That(updatedItems["dummyEmailAddress"], Is.EqualTo("dummy@example.test"));
            });
        }

        [Test]
        public async Task Save_ShowsErrorWhenPersistingConfigFails()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            ThrowingEmailApiConnection apiConnection = new();
            SimulatedGlobalConfig globalConfig = CreateGlobalConfig();
            SimulatedUserConfig userConfig = CreateUserConfig();
            SettingsEmail component = CreateComponent(apiConnection, globalConfig, userConfig, messages);

            await InvokePrivateTask(component, "OnInitializedAsync");

            SetMember(component, "actEmailConnection", new EmailConnection
            {
                ServerAddress = "smtp.example.test",
                Port = 587,
                Encryption = EmailEncryptionMethod.StartTls,
                SenderEmailAddress = "sender@example.test"
            });

            await InvokePrivateTask(component, "Save");

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("Change default"));
                Assert.That(messages[0].Message, Is.Empty);
                Assert.That(messages[0].Exception, Is.TypeOf<InvalidOperationException>());
                Assert.That(messages[0].IsError, Is.True);
            });
        }

        [Test]
        public async Task TestConnection_ShowsErrorWhenNoUserEmailIsConfigured()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            RecordingSettingsApiConn apiConnection = new();
            SimulatedGlobalConfig globalConfig = CreateGlobalConfig();
            SimulatedUserConfig userConfig = CreateUserConfig();
            userConfig.User.Email = "";
            SettingsEmail component = CreateComponent(apiConnection, globalConfig, userConfig, messages);

            await InvokePrivateTask(component, "OnInitializedAsync");
            await InvokePrivateTask(component, "TestConnection");

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("Test email connection"));
                Assert.That(messages[0].Message, Is.EqualTo("No user email configured"));
                Assert.That(messages[0].IsError, Is.True);
            });
        }

        private static BunitContext CreateContext(
            string role,
            RecordingSettingsApiConn apiConnection,
            SimulatedGlobalConfig globalConfig,
            SimulatedUserConfig userConfig)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new AllowAllAuthStateProvider(role));
            context.Services.AddSingleton<DomEventService>();
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddSingleton<GlobalConfig>(globalConfig);
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddSingleton<MiddlewareClient>(new MockMiddlewareClient());
            return context;
        }

        private static IRenderedComponent<CascadingAuthenticationState> RenderComponent(BunitContext context)
        {
            return context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<CascadingValue<Action<Exception?, string, string, bool>>>(child => child
                    .Add(p => p.Value, (_, _, _, _) => { })
                    .AddChildContent<SettingsEmail>()));
        }

        private static (SettingsEmail Component, RecordingSettingsApiConn ApiConnection) CreateInitializedComponent(
            List<(Exception? Exception, string Title, string Message, bool IsError)>? messages = null)
        {
            RecordingSettingsApiConn apiConnection = new();
            SimulatedGlobalConfig globalConfig = CreateGlobalConfig();
            SimulatedUserConfig userConfig = CreateUserConfig();
            SettingsEmail component = CreateComponent(apiConnection, globalConfig, userConfig, messages);
            InvokePrivateTask(component, "OnInitializedAsync").GetAwaiter().GetResult();
            return (component, apiConnection);
        }

        private static SettingsEmail CreateComponent(
            ApiConnection apiConnection,
            GlobalConfig globalConfig,
            UserConfig userConfig,
            List<(Exception? Exception, string Title, string Message, bool IsError)>? messages = null)
        {
            SettingsEmail component = new();
            SetMember(component, "apiConnection", apiConnection);
            SetMember(component, "middlewareClient", new MockMiddlewareClient());
            SetMember(component, "globalConfig", globalConfig);
            SetMember(component, "userConfig", userConfig);
            SetMember(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((exception, title, message, isError) =>
            {
                messages?.Add((exception, title, message, isError));
            }));
            return component;
        }

        private static SimulatedGlobalConfig CreateGlobalConfig()
        {
            return new SimulatedGlobalConfig
            {
                UiLanguages =
                [
                    new Language { Name = GlobalConst.kEnglish, CultureInfo = "en-US" }
                ]
            };
        }

        private static SimulatedUserConfig CreateUserConfig(string role = Roles.Admin)
        {
            SimulatedUserConfig userConfig = new();
            userConfig.User.Roles = [role];
            return userConfig;
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
            MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
            Task task = (Task)(method.Invoke(instance, args) ?? throw new InvalidOperationException($"{methodName} returned null task."));
            await task;
        }

        private sealed class ThrowingEmailApiConnection : SimulatedApiConnection
        {
            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == ConfigQueries.upsertConfigItems)
                {
                    throw new InvalidOperationException("write failed");
                }

                throw new InvalidOperationException($"Unexpected query: {query}");
            }
        }
    }
}
