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
    internal class UiSettingsGatewaysTest
    {
        private static readonly FieldInfo JwtPublicKeyField = typeof(ConfigFile).GetField("jwtPublicKey", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingFieldException(typeof(ConfigFile).FullName, "jwtPublicKey");

        private RsaSecurityKey? originalJwtPublicKey;

        [SetUp]
        public void SetUp()
        {
            originalJwtPublicKey = (RsaSecurityKey?)JwtPublicKeyField.GetValue(null);

            SimulatedUserConfig.DummyTranslate["gateways"] = "Gateways";
            SimulatedUserConfig.DummyTranslate["U5112"] = "Gateway settings";
            SimulatedUserConfig.DummyTranslate["add_new_gateway"] = "Add gateway";
            SimulatedUserConfig.DummyTranslate["actions"] = "Actions";
            SimulatedUserConfig.DummyTranslate["clone"] = "Clone";
            SimulatedUserConfig.DummyTranslate["edit"] = "Edit";
            SimulatedUserConfig.DummyTranslate["delete"] = "Delete";
            SimulatedUserConfig.DummyTranslate["id"] = "Id";
            SimulatedUserConfig.DummyTranslate["name"] = "Name";
            SimulatedUserConfig.DummyTranslate["uid"] = "Uid";
            SimulatedUserConfig.DummyTranslate["type"] = "Type";
            SimulatedUserConfig.DummyTranslate["management"] = "Management";
            SimulatedUserConfig.DummyTranslate["import_enabled"] = "Import enabled";
            SimulatedUserConfig.DummyTranslate["edit_gateway"] = "Edit gateway";
            SimulatedUserConfig.DummyTranslate["save"] = "Save";
            SimulatedUserConfig.DummyTranslate["cancel"] = "Cancel";
            SimulatedUserConfig.DummyTranslate["fetch_gateways"] = "Fetch gateways";
            SimulatedUserConfig.DummyTranslate["save_gateway"] = "Save gateway";
            SimulatedUserConfig.DummyTranslate["delete_gateway"] = "Delete gateway";
            SimulatedUserConfig.DummyTranslate["add_device_to_tenant0"] = "Add device to tenant";
            SimulatedUserConfig.DummyTranslate["U5103"] = "Delete gateway ";
            SimulatedUserConfig.DummyTranslate["E5102"] = "Missing name or reason";
            SimulatedUserConfig.DummyTranslate["E5112"] = "Save gateway failed";
            SimulatedUserConfig.DummyTranslate["U0001"] = "Sanitized input";
        }

        [TearDown]
        public void TearDown()
        {
            JwtPublicKeyField.SetValue(null, originalJwtPublicKey);
        }

        [Test]
        public async Task OnInitializedAsync_LoadsDevicesTypesAndManagementsForAdmin()
        {
            RecordingGatewaysApiConnection apiConnection = CreateApiConnection();

            SettingsGateways component = CreateComponent(apiConnection, await CreateAuthorizedTokenService());

            await InvokePrivateTask(component, "OnInitializedAsync");

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.Queries, Has.Count.EqualTo(3));
                Assert.That(apiConnection.Queries[0], Is.EqualTo(DeviceQueries.getDeviceDetails));
                Assert.That(apiConnection.Queries[1], Is.EqualTo(DeviceQueries.getDeviceTypeDetails));
                Assert.That(apiConnection.Queries[2], Is.EqualTo(DeviceQueries.getManagementsDetails));
                Assert.That(GetMember<List<Device>>(component, "devices"), Has.Count.EqualTo(2));
                Assert.That(GetMember<List<DeviceType>>(component, "deviceTypes"), Has.Count.EqualTo(3));
                Assert.That(GetMember<List<Management>>(component, "managements"), Has.Count.EqualTo(2));
            });
        }

        [Test]
        public async Task OnInitializedAsync_LoadsWithoutSecretsForAuditor()
        {
            RecordingGatewaysApiConnection apiConnection = CreateApiConnection();

            SettingsGateways component = CreateComponent(apiConnection, await CreateAuthorizedTokenService(Roles.Auditor));

            await InvokePrivateTask(component, "OnInitializedAsync");

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.Queries, Has.Count.EqualTo(3));
                Assert.That(apiConnection.Queries[0], Is.EqualTo(DeviceQueries.getDeviceDetails));
                Assert.That(apiConnection.Queries[1], Is.EqualTo(DeviceQueries.getDeviceTypeDetails));
                Assert.That(apiConnection.Queries[2], Is.EqualTo(DeviceQueries.getManagementDetailsWithoutSecrets));
                Assert.That(GetMember<List<Management>>(component, "managements"), Has.Count.EqualTo(2));
            });
        }

        [Test]
        public async Task OnInitializedAsync_ShowsMessageWhenDeviceLoadFails()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = new();
            RecordingGatewaysApiConnection apiConnection = CreateApiConnection();
            apiConnection.ThrowOnDeviceQuery = true;

            SettingsGateways component = CreateComponent(apiConnection, await CreateAuthorizedTokenService(), messages);

            await InvokePrivateTask(component, "OnInitializedAsync");

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("Fetch gateways"));
                Assert.That(messages[0].Message, Is.Empty);
                Assert.That(messages[0].Exception, Is.TypeOf<InvalidOperationException>());
                Assert.That(apiConnection.Queries, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public void Edit_SwitchesDeviceTypeToMatchingManufacturer()
        {
            RecordingGatewaysApiConnection apiConnection = CreateApiConnection();
            SettingsGateways component = CreateComponent(apiConnection, new TokenService(new MockMiddlewareClient(), new MockProtectedSessionStorage()));

            SetMember(component, "deviceTypes", new List<DeviceType>
            {
                BuildDeviceType(10, "FortiGate", "7", "Fortinet"),
                BuildDeviceType(12, "FortiManager", "7", "Fortinet"),
                BuildDeviceType(20, "Cisco FTD", "7", "Cisco")
            });
            SetMember(component, "managements", new List<Management>
            {
                BuildManagement(1, "fortinet-mgr", 12, "Fortinet"),
                BuildManagement(2, "cisco-mgr", 20, "Cisco")
            });

            Device device = new()
            {
                Id = 99,
                Name = "gateway99",
                Uid = "gateway99",
                Management = BuildManagement(1, "fortinet-mgr", 12, "Fortinet"),
                DeviceType = BuildDeviceType(20, "Cisco FTD", "7", "Cisco")
            };

            InvokePrivateVoid(component, "Edit", device);

            Assert.Multiple(() =>
            {
                Assert.That(GetMember<bool>(component, "EditMode"), Is.True);
                Assert.That(GetMember<Device>(component, "actDevice").DeviceType.Id, Is.EqualTo(10));
            });
        }

        [Test]
        public void Add_PreparesNewGatewayFromFirstEntries()
        {
            RecordingGatewaysApiConnection apiConnection = CreateApiConnection();
            SettingsGateways component = CreateComponent(apiConnection, new TokenService(new MockMiddlewareClient(), new MockProtectedSessionStorage()));

            SetMember(component, "deviceTypes", new List<DeviceType>
            {
                BuildDeviceType(10, "FortiGate", "7", "Fortinet"),
                BuildDeviceType(12, "FortiManager", "7", "Fortinet")
            });
            SetMember(component, "managements", new List<Management>
            {
                BuildManagement(1, "fortinet-mgr", 12, "Fortinet")
            });

            InvokePrivateVoid(component, "Add");

            Assert.Multiple(() =>
            {
                Assert.That(GetMember<bool>(component, "AddMode"), Is.True);
                Assert.That(GetMember<bool>(component, "EditMode"), Is.True);
                Assert.That(GetMember<Device>(component, "actDevice").DeviceType.Id, Is.EqualTo(10));
                Assert.That(GetMember<Device>(component, "actDevice").Management.Id, Is.EqualTo(1));
            });
        }

        [Test]
        public void Clone_CreatesEditableCopyWithResetId()
        {
            RecordingGatewaysApiConnection apiConnection = CreateApiConnection();
            SettingsGateways component = CreateComponent(apiConnection, new TokenService(new MockMiddlewareClient(), new MockProtectedSessionStorage()));

            SetMember(component, "deviceTypes", new List<DeviceType>
            {
                BuildDeviceType(10, "FortiGate", "7", "Fortinet"),
                BuildDeviceType(20, "Cisco FTD", "7", "Cisco")
            });
            SetMember(component, "managements", new List<Management>
            {
                BuildManagement(1, "fortinet-mgr", 12, "Fortinet"),
                BuildManagement(2, "cisco-mgr", 20, "Cisco")
            });

            Device device = new()
            {
                Id = 44,
                Name = "clone-me",
                Uid = "clone-me",
                Management = BuildManagement(2, "cisco-mgr", 20, "Cisco"),
                DeviceType = BuildDeviceType(20, "Cisco FTD", "7", "Cisco")
            };

            InvokePrivateVoid(component, "Clone", device);

            Assert.Multiple(() =>
            {
                Assert.That(GetMember<bool>(component, "AddMode"), Is.True);
                Assert.That(GetMember<bool>(component, "EditMode"), Is.True);
                Assert.That(GetMember<Device>(component, "actDevice").Id, Is.EqualTo(0));
                Assert.That(GetMember<Device>(component, "actDevice").Name, Is.EqualTo("clone-me"));
            });
        }

        [Test]
        public async Task Save_ShowsValidationWhenNameMissing()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = new();
            RecordingGatewaysApiConnection apiConnection = CreateApiConnection();
            SettingsGateways component = CreateComponent(apiConnection, await CreateAuthorizedTokenService(), messages);

            await InvokePrivateTask(component, "OnInitializedAsync");
            InvokePrivateVoid(component, "Add");
            SetMember(component, "actDevice", new Device
            {
                Name = "",
                Uid = "gateway-new",
                DeviceType = BuildDeviceType(10, "FortiGate", "7", "Fortinet"),
                Management = BuildManagement(1, "fortinet-mgr", 12, "Fortinet")
            });

            await InvokePrivateTask(component, "Save");

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("Save gateway"));
                Assert.That(messages[0].Message, Is.EqualTo("Missing name or reason"));
                Assert.That(apiConnection.Queries.Count(query => query == DeviceQueries.newDevice), Is.EqualTo(0));
                Assert.That(apiConnection.Queries.Count(query => query == DeviceQueries.updateDevice), Is.EqualTo(0));
            });
        }

        [Test]
        public async Task Save_AllowsLegacyGatewayNameWithSpaces()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = new();
            RecordingGatewaysApiConnection apiConnection = CreateApiConnection();
            apiConnection.NewDeviceResult = new ReturnIdWrapper
            {
                ReturnIds = new ReturnId[] { new ReturnId { NewId = 88 } }
            };
            SettingsGateways component = CreateComponent(apiConnection, await CreateAuthorizedTokenService(), messages);

            await InvokePrivateTask(component, "OnInitializedAsync");
            InvokePrivateVoid(component, "Add");
            SetMember(component, "actDevice", new Device
            {
                Name = "legacy gateway",
                Uid = "legacy-gateway",
                DeviceType = BuildDeviceType(7, "Check Point", "R77", "Check Point"),
                Management = BuildManagement(1, "fortinet-mgr", 12, "Fortinet")
            });

            await InvokePrivateTask(component, "Save");

            Assert.Multiple(() =>
            {
                Assert.That(messages, Is.Empty);
                Assert.That(apiConnection.Queries.Count(query => query == DeviceQueries.newDevice), Is.EqualTo(1));
                Assert.That(GetMember<List<Device>>(component, "devices"), Has.Count.EqualTo(3));
                Assert.That(GetMember<List<Device>>(component, "devices")[2].Id, Is.EqualTo(88));
            });
        }

        [Test]
        public async Task Save_AddMode_PersistsGatewayAndAddsTenantLink()
        {
            RecordingGatewaysApiConnection apiConnection = CreateApiConnection();
            apiConnection.NewDeviceResult = new ReturnIdWrapper
            {
                ReturnIds = new ReturnId[] { new ReturnId { NewId = 77 } }
            };

            SettingsGateways component = CreateComponent(apiConnection, await CreateAuthorizedTokenService());

            await InvokePrivateTask(component, "OnInitializedAsync");
            InvokePrivateVoid(component, "Add");
            SetMember(component, "actDevice", new Device
            {
                Name = "new-gateway",
                Uid = "new-gateway",
                DeviceType = BuildDeviceType(10, "FortiGate", "7", "Fortinet"),
                Management = BuildManagement(1, "fortinet-mgr", 12, "Fortinet"),
                ImportDisabled = false,
                HideInUi = false
            });

            await InvokePrivateTask(component, "Save");

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.Queries.Count(query => query == DeviceQueries.newDevice), Is.EqualTo(1));
                Assert.That(apiConnection.Queries.Count(query => query == AuthQueries.addDeviceToTenant), Is.EqualTo(1));
                Assert.That(GetMember<List<Device>>(component, "devices"), Has.Count.EqualTo(3));
                Assert.That(GetMember<List<Device>>(component, "devices")[2].Id, Is.EqualTo(77));
                Assert.That(GetMember<bool>(component, "AddMode"), Is.False);
                Assert.That(GetMember<bool>(component, "EditMode"), Is.False);
            });
        }

        [Test]
        public async Task Save_AddMode_ShowsMessageWhenInsertFails()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = new();
            RecordingGatewaysApiConnection apiConnection = CreateApiConnection();
            apiConnection.NewDeviceResult = new ReturnIdWrapper
            {
                ReturnIds = null
            };

            SettingsGateways component = CreateComponent(apiConnection, await CreateAuthorizedTokenService(), messages);

            await InvokePrivateTask(component, "OnInitializedAsync");
            InvokePrivateVoid(component, "Add");
            SetMember(component, "actDevice", new Device
            {
                Name = "new-gateway",
                Uid = "new-gateway",
                DeviceType = BuildDeviceType(10, "FortiGate", "7", "Fortinet"),
                Management = BuildManagement(1, "fortinet-mgr", 12, "Fortinet")
            });

            await InvokePrivateTask(component, "Save");

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("Save gateway"));
                Assert.That(messages[0].Message, Is.EqualTo("Save gateway failed"));
                Assert.That(GetMember<bool>(component, "AddMode"), Is.True);
                Assert.That(GetMember<bool>(component, "EditMode"), Is.True);
            });
        }

        [Test]
        public async Task Save_UpdatesExistingGatewaySuccessfully()
        {
            RecordingGatewaysApiConnection apiConnection = CreateApiConnection();
            apiConnection.UpdateDeviceResult = new ReturnId { UpdatedId = 1 };

            SettingsGateways component = CreateComponent(apiConnection, await CreateAuthorizedTokenService());

            await InvokePrivateTask(component, "OnInitializedAsync");
            InvokePrivateVoid(component, "Edit", GetMember<List<Device>>(component, "devices")[0]);
            SetMember(component, "actDevice", new Device
            {
                Id = 1,
                Name = "gateway1-renamed",
                Uid = "gateway1-renamed",
                DeviceType = BuildDeviceType(10, "FortiGate", "7", "Fortinet"),
                Management = BuildManagement(1, "fortinet-mgr", 12, "Fortinet")
            });

            await InvokePrivateTask(component, "Save");

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.Queries.Count(query => query == DeviceQueries.updateDevice), Is.EqualTo(1));
                Assert.That(GetMember<bool>(component, "EditMode"), Is.False);
                Assert.That(GetMember<List<Device>>(component, "devices")[0].Name, Is.EqualTo("gateway1-renamed"));
            });
        }

        [Test]
        public async Task Save_UpdateMode_StaysInEditModeWhenUpdateNotAccepted()
        {
            RecordingGatewaysApiConnection apiConnection = CreateApiConnection();
            apiConnection.UpdateDeviceResult = new ReturnId { UpdatedId = 999 };

            SettingsGateways component = CreateComponent(apiConnection, await CreateAuthorizedTokenService());

            await InvokePrivateTask(component, "OnInitializedAsync");
            List<Device> devices = GetMember<List<Device>>(component, "devices");
            InvokePrivateVoid(component, "Edit", devices[0]);
            SetMember(component, "actDevice", new Device
            {
                Id = 1,
                Name = "gateway1-renamed",
                Uid = "gateway1-renamed",
                DeviceType = BuildDeviceType(10, "FortiGate", "7", "Fortinet"),
                Management = BuildManagement(1, "fortinet-mgr", 12, "Fortinet")
            });

            await InvokePrivateTask(component, "Save");

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.Queries.Count(query => query == DeviceQueries.updateDevice), Is.EqualTo(1));
                Assert.That(GetMember<bool>(component, "EditMode"), Is.True);
                Assert.That(GetMember<List<Device>>(component, "devices")[0].Name, Is.EqualTo("gateway1-renamed"));
            });
        }

        private static RecordingGatewaysApiConnection CreateApiConnection()
        {
            return new RecordingGatewaysApiConnection
            {
                Devices = new List<Device>
                {
                    new Device
                    {
                        Id = 1,
                        Name = "gateway1",
                        Uid = "gateway1",
                        DeviceType = BuildDeviceType(10, "FortiGate", "7", "Fortinet"),
                        Management = BuildManagement(1, "fortinet-mgr", 12, "Fortinet"),
                        ImportDisabled = false,
                        HideInUi = false
                    },
                    new Device
                    {
                        Id = 2,
                        Name = "gateway2",
                        Uid = "gateway2",
                        DeviceType = BuildDeviceType(20, "Cisco FTD", "7", "Cisco"),
                        Management = BuildManagement(2, "cisco-mgr", 20, "Cisco"),
                        ImportDisabled = true,
                        HideInUi = true
                    }
                },
                DeviceTypes = new List<DeviceType>
                {
                    BuildDeviceType(10, "FortiGate", "7", "Fortinet"),
                    BuildDeviceType(12, "FortiManager", "7", "Fortinet"),
                    BuildDeviceType(20, "Cisco FTD", "7", "Cisco")
                },
                Managements = new List<Management>
                {
                    BuildManagement(1, "fortinet-mgr", 12, "Fortinet"),
                    BuildManagement(2, "cisco-mgr", 20, "Cisco")
                }
            };
        }

        private static SettingsGateways CreateComponent(RecordingGatewaysApiConnection apiConnection, TokenService tokenService, List<(Exception? Exception, string Title, string Message, bool IsError)>? messages = null)
        {
            SettingsGateways component = new();
            SetMember(component, "apiConnection", apiConnection);
            SetMember(component, "userConfig", new SimulatedUserConfig
            {
                User =
                {
                    DbId = 7,
                    Roles = new List<string> { Roles.Admin }
                }
            });
            SetMember(component, "tokenService", tokenService);
            SetMember(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((exception, title, message, isError) =>
            {
                messages?.Add((exception, title, message, isError));
            }));
            return component;
        }

        private static async Task<TokenService> CreateAuthorizedTokenService(string role = Roles.Admin)
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
                Roles = new List<string> { role }
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

        private static Management BuildManagement(int id, string name, int deviceTypeId, string manufacturer)
        {
            return new Management
            {
                Id = id,
                Name = name,
                Hostname = $"{name}.example.org",
                Port = 443,
                DeviceType = BuildDeviceType(deviceTypeId, $"{manufacturer} manager", "7", manufacturer)
            };
        }

        private static DeviceType BuildDeviceType(int id, string name, string version, string manufacturer)
        {
            return new DeviceType
            {
                Id = id,
                Name = name,
                Version = version,
                Manufacturer = manufacturer,
                IsManagement = true
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

        private sealed class RecordingGatewaysApiConnection : SimulatedApiConnection
        {
            public List<string> Queries { get; } = new();
            public List<object?> Variables { get; } = new();
            public List<Device> Devices { get; set; } = new();
            public List<DeviceType> DeviceTypes { get; set; } = new();
            public List<Management> Managements { get; set; } = new();
            public ReturnIdWrapper NewDeviceResult { get; set; } = new();
            public ReturnId UpdateDeviceResult { get; set; } = new();
            public ReturnId DeleteDeviceResult { get; set; } = new();
            public bool ThrowOnDeviceQuery { get; set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                Queries.Add(query);
                Variables.Add(variables);

                if (ThrowOnDeviceQuery && query == DeviceQueries.getDeviceDetails)
                {
                    throw new InvalidOperationException("gateway load failed");
                }

                if (query == DeviceQueries.getDeviceDetails && typeof(QueryResponseType) == typeof(List<Device>))
                {
                    return Task.FromResult((QueryResponseType)(object)Devices.Select(item => new Device(item)).ToList());
                }

                if (query == DeviceQueries.getDeviceTypeDetails && typeof(QueryResponseType) == typeof(List<DeviceType>))
                {
                    return Task.FromResult((QueryResponseType)(object)DeviceTypes.Select(item => new DeviceType(item)).ToList());
                }

                if ((query == DeviceQueries.getManagementsDetails || query == DeviceQueries.getManagementDetailsWithoutSecrets) &&
                    typeof(QueryResponseType) == typeof(List<Management>))
                {
                    return Task.FromResult((QueryResponseType)(object)Managements.Select(item => new Management(item)).ToList());
                }

                if (query == DeviceQueries.newDevice && typeof(QueryResponseType) == typeof(ReturnIdWrapper))
                {
                    return Task.FromResult((QueryResponseType)(object)NewDeviceResult);
                }

                if (query == DeviceQueries.updateDevice && typeof(QueryResponseType) == typeof(ReturnId))
                {
                    return Task.FromResult((QueryResponseType)(object)UpdateDeviceResult);
                }

                if (query == DeviceQueries.deleteDevice && typeof(QueryResponseType) == typeof(ReturnId))
                {
                    return Task.FromResult((QueryResponseType)(object)DeleteDeviceResult);
                }

                if (query == AuthQueries.addDeviceToTenant && typeof(QueryResponseType) == typeof(ReturnIdWrapper))
                {
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper { ReturnIds = Array.Empty<ReturnId>() });
                }

                throw new InvalidOperationException($"Unexpected query: {query}");
            }
        }
    }
}
