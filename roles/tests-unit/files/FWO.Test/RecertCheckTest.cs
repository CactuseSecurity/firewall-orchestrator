using System.Reflection;
using System.Text.Json;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Report;
using FWO.Data.Workflow;
using FWO.Middleware.Server;
using FWO.Middleware.Server.Services;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class RecertCheckTest
    {
        private static readonly string kUpcomingText = "upcoming for " + Placeholder.APPNAME;
        private static readonly string kOverdueText = "overdue for " + Placeholder.APPNAME;
        private static readonly string[] kExpectedQueries =
        [
            OwnerQueries.getOwners
        ];

        [Test]
        public async Task CheckRecertifications_ReturnsZeroWhenInternalGroupsAreMissing()
        {
            RecertCheckApiConnection apiConnection = new()
            {
                Owners =
                [
                    new FwoOwner { Id = 1, Name = "Owner A", RecertActive = true }
                ]
            };
            RecertCheck recertCheck = CreateRecertCheck(apiConnection, CreateGlobalConfig());

            int emailsSent = await recertCheck.CheckRecertifications();

            Assert.Multiple(() =>
            {
                Assert.That(emailsSent, Is.Zero);
                Assert.That(apiConnection.Queries, Is.SupersetOf(kExpectedQueries));
            });
        }

        [Test]
        public async Task CheckRecertifications_ReturnsZeroWhenOwnerLoadingThrows()
        {
            RecertCheckApiConnection apiConnection = new()
            {
                ThrowOnOwners = true
            };
            RecertCheck recertCheck = CreateRecertCheck(apiConnection, CreateGlobalConfig());

            int emailsSent = await recertCheck.CheckRecertifications();

            Assert.Multiple(() =>
            {
                Assert.That(emailsSent, Is.Zero);
                Assert.That(apiConnection.Queries, Has.Count.EqualTo(1));
                Assert.That(apiConnection.Queries[0], Is.EqualTo(OwnerQueries.getOwners));
            });
        }

        [Test]
        public async Task InitEnv_LoadsGlobalParamsAndQueryData()
        {
            RecertCheckApiConnection apiConnection = new()
            {
                Owners =
                [
                    new FwoOwner { Id = 7, Name = "Owner B" }
                ]
            };
            GlobalConfig globalConfig = CreateGlobalConfig();
            RecertCheck recertCheck = CreateRecertCheck(apiConnection, globalConfig);

            await InvokePrivateTask(recertCheck, "InitEnv");

            RecertCheckParams? loadedParams = GetPrivateField<RecertCheckParams?>(recertCheck, "globCheckParams");
            List<FwoOwner> owners = GetPrivateField<List<FwoOwner>>(recertCheck, "owners");

            Assert.Multiple(() =>
            {
                Assert.That(loadedParams, Is.Not.Null);
                Assert.That(loadedParams!.RecertCheckInterval, Is.EqualTo(SchedulerInterval.Days));
                Assert.That(loadedParams.RecertCheckOffset, Is.EqualTo(7));
                Assert.That(owners, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public void IsRecertCheckTime_ReturnsTrueForDueDaysAndFalseForInactiveOwners()
        {
            RecertCheck recertCheck = CreateRecertCheck(new RecertCheckApiConnection(), CreateGlobalConfig());
            SetPrivateField(recertCheck, "globCheckParams", new RecertCheckParams
            {
                RecertCheckInterval = SchedulerInterval.Days,
                RecertCheckOffset = 7
            });

            FwoOwner dueOwner = new()
            {
                RecertActive = true,
                LastRecertCheck = new DateTime(2026, 7, 13)
            };
            FwoOwner inactiveOwner = new()
            {
                RecertActive = false,
                LastRecertCheck = new DateTime(2026, 7, 13)
            };

            bool due = InvokePrivate<bool>(recertCheck, "IsRecertCheckTime", dueOwner);
            bool inactive = InvokePrivate<bool>(recertCheck, "IsRecertCheckTime", inactiveOwner);

            Assert.Multiple(() =>
            {
                Assert.That(due, Is.True);
                Assert.That(inactive, Is.False);
            });
        }

        [Test]
        public void IsRecertCheckTime_UsesOwnerSpecificParameters()
        {
            RecertCheck recertCheck = CreateRecertCheck(new RecertCheckApiConnection(), CreateGlobalConfig());
            SetPrivateField(recertCheck, "globCheckParams", new RecertCheckParams
            {
                RecertCheckInterval = SchedulerInterval.Days,
                RecertCheckOffset = 14
            });

            FwoOwner owner = new()
            {
                RecertActive = true,
                LastRecertCheck = new DateTime(2026, 7, 13),
                RecertCheckParamString = JsonSerializer.Serialize(new RecertCheckParams
                {
                    RecertCheckInterval = SchedulerInterval.Weeks,
                    RecertCheckOffset = 1,
                    RecertCheckWeekday = (int)DayOfWeek.Monday
                })
            };

            bool due = InvokePrivate<bool>(recertCheck, "IsRecertCheckTime", owner);

            Assert.That(due, Is.True);
        }

        [Test]
        public void CalcForWeeks_JumpsToConfiguredWeekday()
        {
            RecertCheckParams checkParams = new()
            {
                RecertCheckOffset = 1,
                RecertCheckWeekday = (int)DayOfWeek.Friday
            };

            DateTime nextCheck = InvokePrivate<DateTime>(typeof(RecertCheck), "CalcForWeeks", new DateTime(2026, 7, 20), checkParams);

            Assert.That(nextCheck, Is.EqualTo(new DateTime(2026, 7, 24)));
        }

        [Test]
        public void CalcForWeeks_UsesOffsetWhenWeekdayIsUnset()
        {
            RecertCheckParams checkParams = new()
            {
                RecertCheckOffset = 2
            };

            DateTime nextCheck = InvokePrivate<DateTime>(typeof(RecertCheck), "CalcForWeeks", new DateTime(2026, 7, 20), checkParams);

            Assert.That(nextCheck, Is.EqualTo(new DateTime(2026, 8, 3)));
        }

        [Test]
        public void CalcForMonths_FallsBackToNextMonthWhenConfiguredDayIsMissing()
        {
            RecertCheckParams checkParams = new()
            {
                RecertCheckOffset = 1,
                RecertCheckDayOfMonth = 31
            };

            DateTime nextCheck = InvokePrivate<DateTime>(typeof(RecertCheck), "CalcForMonths", new DateTime(2026, 1, 31), checkParams);

            Assert.That(nextCheck.Date, Is.EqualTo(new DateTime(2026, 3, 1)));
        }

        [Test]
        public void CalcForMonths_UsesOffsetWhenDayOfMonthIsUnset()
        {
            RecertCheckParams checkParams = new()
            {
                RecertCheckOffset = 3
            };

            DateTime nextCheck = InvokePrivate<DateTime>(typeof(RecertCheck), "CalcForMonths", new DateTime(2026, 1, 31), checkParams);

            Assert.That(nextCheck.Date, Is.EqualTo(new DateTime(2026, 4, 30)));
        }

        [Test]
        public void IsRecertCheckTime_ReturnsFalseForFutureDates()
        {
            RecertCheck recertCheck = CreateRecertCheck(new RecertCheckApiConnection(), CreateGlobalConfig());
            SetPrivateField(recertCheck, "globCheckParams", new RecertCheckParams
            {
                RecertCheckInterval = SchedulerInterval.Days,
                RecertCheckOffset = 30
            });

            FwoOwner owner = new()
            {
                RecertActive = true,
                LastRecertCheck = DateTime.Today
            };

            bool due = InvokePrivate<bool>(recertCheck, "IsRecertCheckTime", owner);

            Assert.That(due, Is.False);
        }

        [Test]
        public void IsRecertCheckTime_ThrowsForUnsupportedInterval()
        {
            RecertCheck recertCheck = CreateRecertCheck(new RecertCheckApiConnection(), CreateGlobalConfig());
            SetPrivateField(recertCheck, "globCheckParams", new RecertCheckParams
            {
                RecertCheckInterval = (SchedulerInterval)999,
                RecertCheckOffset = 1
            });

            FwoOwner owner = new()
            {
                RecertActive = true,
                LastRecertCheck = DateTime.Today
            };

            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() => InvokePrivate<bool>(recertCheck, "IsRecertCheckTime", owner))!;
            Assert.That(exception.InnerException, Is.TypeOf<NotSupportedException>());
        }

        [Test]
        public void PrepareOwnerBody_ChoosesUpcomingAndOverdueMessages()
        {
            SimulatedGlobalConfig globalConfig = CreateGlobalConfig();
            globalConfig.RecCheckEmailUpcomingText = kUpcomingText;
            globalConfig.RecCheckEmailOverdueText = kOverdueText;
            RecertCheck recertCheck = CreateRecertCheck(new RecertCheckApiConnection(), globalConfig);

            string upcoming = InvokePrivate<string>(recertCheck, "PrepareOwnerBody", new FwoOwner
            {
                Name = "Owner A",
                NextRecertDate = DateTime.Today.AddDays(1)
            });
            string overdue = InvokePrivate<string>(recertCheck, "PrepareOwnerBody", new FwoOwner
            {
                Name = "Owner B",
                NextRecertDate = DateTime.Today.AddDays(-1)
            });

            Assert.Multiple(() =>
            {
                Assert.That(upcoming, Does.Contain("Owner A"));
                Assert.That(upcoming, Does.Contain("upcoming"));
                Assert.That(overdue, Does.Contain("Owner B"));
                Assert.That(overdue, Does.Contain("overdue"));
            });
        }

        [Test]
        public void PrepareRulesBody_FormatsUpcomingAndOverdueSections()
        {
            SimulatedGlobalConfig globalConfig = CreateGlobalConfig();
            globalConfig.RecCheckEmailUpcomingText = kUpcomingText;
            globalConfig.RecCheckEmailOverdueText = kOverdueText;
            RecertCheck recertCheck = CreateRecertCheck(new RecertCheckApiConnection(), globalConfig);
            Rule upcomingRule = CreateRule("Upcoming Rule", "UID-UP", new DateTime(2026, 7, 24));
            Rule overdueRule = CreateRule("Overdue Rule", "UID-OVER", new DateTime(2026, 7, 10));

            string body = InvokePrivate<string>(recertCheck, "PrepareRulesBody", new List<Rule> { upcomingRule }, new List<Rule> { overdueRule }, "Owner A");

            Assert.Multiple(() =>
            {
                Assert.That(body, Does.Contain("Owner A"));
                Assert.That(body, Does.Contain("upcoming"));
                Assert.That(body, Does.Contain("overdue"));
                Assert.That(body, Does.Contain("Upcoming Rule"));
                Assert.That(body, Does.Contain("Overdue Rule"));
                Assert.That(body, Does.Contain("UID-UP"));
                Assert.That(body, Does.Contain("UID-OVER"));
            });
        }

        [Test]
        public void PrepareLine_UsesEmptyDatePrefixWhenRecertDateMissing()
        {
            Rule rule = CreateRule("No date rule", "UID-NO-DATE", new DateTime(2026, 7, 24));
            rule.Metadata.RuleRecertification[0].NextRecertDate = null;

            string line = InvokePrivate<string>(typeof(RecertCheck), "PrepareLine", rule);

            Assert.That(line, Does.StartWith(": Device A: No date rule:UID-NO-DATE"));
        }

        [Test]
        public async Task GenerateRulesRecertificationReport_ReturnsEmptyWhenDeviceLookupThrows()
        {
            RecertCheckApiConnection apiConnection = new()
            {
                ThrowOnDevicesQuery = true
            };
            RecertCheck recertCheck = CreateRecertCheck(apiConnection, CreateGlobalConfig());

            List<Rule> result = await InvokePrivateAsync<List<Rule>>(recertCheck, "GenerateRulesRecertificationReport", apiConnection, new FwoOwner
            {
                Id = 7,
                Name = "Owner A"
            });

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Empty);
                Assert.That(apiConnection.Queries, Does.Contain(ConfigQueries.getConfigItemsByUser));
                Assert.That(apiConnection.Queries, Does.Contain(ConfigQueries.getCustomTextsPerLanguage));
                Assert.That(apiConnection.Queries, Does.Contain(DeviceQueries.getDevicesByManagement));
            });
        }

        [Test]
        public async Task SetOwnerLastCheck_SendsMutationWithOwnerId()
        {
            RecertCheckApiConnection apiConnection = new();
            RecertCheck recertCheck = CreateRecertCheck(apiConnection, CreateGlobalConfig());
            FwoOwner owner = new() { Id = 42 };

            await InvokePrivateTask(recertCheck, "SetOwnerLastCheck", owner);

            Assert.That(apiConnection.Queries, Does.Contain(OwnerQueries.setOwnerLastCheck));
        }

        private static SimulatedGlobalConfig CreateGlobalConfig()
        {
            return new SimulatedGlobalConfig
            {
                RecCheckParams = JsonSerializer.Serialize(new RecertCheckParams
                {
                    RecertCheckInterval = SchedulerInterval.Days,
                    RecertCheckOffset = 7
                }),
                RecCheckEmailSubject = "Recertification check",
                RecCheckEmailUpcomingText = kUpcomingText,
                RecCheckEmailOverdueText = kOverdueText,
                DefaultLanguage = GlobalConst.kEnglish,
                RecertificationMode = RecertificationMode.OwnersAndRules,
                UseDummyEmailAddress = true,
                DummyEmailAddress = "dummy@example.test"
            };
        }

        private static RecertCheck CreateRecertCheck(RecertCheckApiConnection apiConnection, GlobalConfig globalConfig)
        {
            return new RecertCheck(apiConnection, globalConfig, new TokenLifetimeProvider());
        }

        private static T InvokePrivate<T>(object instance, string methodName, params object?[] parameters)
        {
            MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
            object? result = method.Invoke(instance, parameters);
            return result is T typedResult ? typedResult : throw new InvalidOperationException($"Unexpected result type for {methodName}.");
        }

        private static T InvokePrivate<T>(Type type, string methodName, params object?[] parameters)
        {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(type.FullName, methodName);
            object? result = method.Invoke(null, parameters);
            return result is T typedResult ? typedResult : throw new InvalidOperationException($"Unexpected result type for {methodName}.");
        }

        private static Task<T> InvokePrivateAsync<T>(object instance, string methodName, params object?[] parameters)
        {
            MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
            object? result = method.Invoke(instance, parameters);
            return result is Task<T> typedTask ? typedTask : throw new InvalidOperationException($"Unexpected task type for {methodName}.");
        }

        private static Task InvokePrivateTask(object instance, string methodName, params object?[] parameters)
        {
            MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
            object? result = method.Invoke(instance, parameters);
            return result is Task typedTask ? typedTask : throw new InvalidOperationException($"Unexpected task type for {methodName}.");
        }

        private static void SetPrivateField<T>(object instance, string fieldName, T value)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(instance.GetType().FullName, fieldName);
            field.SetValue(instance, value);
        }

        private static T GetPrivateField<T>(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(instance.GetType().FullName, fieldName);
            return (T)field.GetValue(instance)!;
        }

        private static Rule CreateRule(string name, string ruleUid, DateTime nextRecertDate)
        {
            return new Rule
            {
                Name = name,
                Uid = ruleUid,
                DeviceName = "Device A",
                Metadata =
                {
                    RuleRecertification =
                    [
                        new Recertification
                        {
                            RecertDate = null,
                            NextRecertDate = nextRecertDate
                        }
                    ]
                }
            };
        }

        private sealed class RecertCheckApiConnection : JobTestApiConnectionBase
        {
            public bool ThrowOnOwners { get; set; }
            public bool ThrowOnDevicesQuery { get; set; }
            public List<FwoOwner> Owners { get; set; } = [];
            public List<ManagementSelect> Managements { get; set; } = [];

            protected override Task<QueryResponseType> HandleQueryAsync<QueryResponseType>(
                string query,
                object? variables,
                string? operationName,
                QueryChunkingOptions? chunkingOptions)
            {
                if (query == ConfigQueries.getConfigItemsByUser && typeof(QueryResponseType) == typeof(ConfigItem[]))
                {
                    return Task.FromResult((QueryResponseType)(object)Array.Empty<ConfigItem>());
                }

                if (query == ConfigQueries.getCustomTextsPerLanguage && typeof(QueryResponseType) == typeof(List<UiText>))
                {
                    return Task.FromResult((QueryResponseType)(object)new List<UiText>());
                }

                if (query == OwnerQueries.getOwners && typeof(QueryResponseType) == typeof(List<FwoOwner>))
                {
                    if (ThrowOnOwners)
                    {
                        throw new InvalidOperationException("boom");
                    }
                    return Task.FromResult((QueryResponseType)(object)Owners);
                }

                if (query == DeviceQueries.getDevicesByManagement && typeof(QueryResponseType) == typeof(List<ManagementSelect>))
                {
                    if (ThrowOnDevicesQuery)
                    {
                        throw new InvalidOperationException("boom");
                    }
                    return Task.FromResult((QueryResponseType)(object)Managements);
                }

                if (query == OwnerQueries.setOwnerLastCheck)
                {
                    return Task.FromResult(default(QueryResponseType)!);
                }

                throw new InvalidOperationException($"Unexpected query in recert check test: {query}");
            }
        }
    }
}
