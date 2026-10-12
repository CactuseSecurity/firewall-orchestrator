using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Recert;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    internal class RecertHandlerTest
    {
        [Test]
        public async Task RecertifySingleRule_QueuesNextRecertificationForRecertRules()
        {
            RecordingRecertApiConnection apiConnection = new();
            SimulatedUserConfig userConfig = CreateUserConfig();
            RecertHandler handler = new(apiConnection, userConfig);
            FwoOwner owner = new()
            {
                Id = 7,
                Name = "Owner A",
                RecertInterval = 14
            };
            Rule rule = new()
            {
                Id = 17,
                Uid = "rule-17",
                RulebaseId = 3,
                Metadata = new RuleMetadata
                {
                    Id = 99,
                    Recert = true
                }
            };

            bool success = await handler.RecertifySingleRule(rule, owner, "comment");

            Assert.Multiple(() =>
            {
                Assert.That(success, Is.True);
                Assert.That(apiConnection.CountQuery(RecertQueries.recertify), Is.EqualTo(1));
                Assert.That(apiConnection.CountQuery(RecertQueries.prepareNextRecertification), Is.EqualTo(1));
                Assert.That(apiConnection.CountQuery(OwnerQueries.setOwnerLastRecert), Is.EqualTo(0));
            });
        }

        [Test]
        public async Task RecertifyOwnerWithRules_RecertifiesOwnerAndRules()
        {
            RecordingRecertApiConnection apiConnection = new();
            SimulatedUserConfig userConfig = CreateUserConfig();
            RecertHandler handler = new(apiConnection, userConfig);
            FwoOwner owner = new()
            {
                Id = 7,
                Name = "Owner A",
                RecertInterval = 30
            };
            List<Rule> rules = new()
            {
                CreateRule(101),
                CreateRule(102)
            };

            FwoOwner recertifiedOwner = await handler.RecertifyOwnerWithRules(owner, rules, "comment");

            Assert.Multiple(() =>
            {
                Assert.That(recertifiedOwner.LastRecertId, Is.EqualTo(1234));
                Assert.That(recertifiedOwner.LastRecertifierDn, Is.EqualTo(userConfig.User.Dn));
                Assert.That(recertifiedOwner.LastRecertifierId, Is.EqualTo(userConfig.User.DbId));
                Assert.That(recertifiedOwner.NextRecertDate, Is.Not.Null);
                Assert.That(apiConnection.CountQuery(RecertQueries.recertifyOwner), Is.EqualTo(1));
                Assert.That(apiConnection.CountQuery(OwnerQueries.setOwnerLastRecert), Is.EqualTo(1));
                Assert.That(apiConnection.CountQuery(RecertQueries.recertifyRuleDirectly), Is.EqualTo(2));
            });
        }

        [Test]
        public async Task InitOwnerRecert_SkipsWhenInitialRecertAlreadyExists()
        {
            RecordingRecertApiConnection apiConnection = new();
            apiConnection.OwnerRecerts.Add(new OwnerRecertification
            {
                Id = 1,
                OwnerId = 7
            });
            SimulatedUserConfig userConfig = CreateUserConfig();
            RecertHandler handler = new(apiConnection, userConfig);

            await handler.InitOwnerRecert(new FwoOwner { Id = 7, Name = "Owner A" });

            Assert.That(apiConnection.CountQuery(RecertQueries.recertifyOwner), Is.EqualTo(0));
        }

        private static SimulatedUserConfig CreateUserConfig()
        {
            return new SimulatedUserConfig
            {
                RecertificationPeriod = 30,
                InitialRecertificationPeriod = 14,
                InitialRecertifier = "cn=initial,dc=test",
                User =
                {
                    DbId = 77,
                    Dn = "cn=recertifier,dc=test"
                }
            };
        }

        private static Rule CreateRule(long id)
        {
            return new Rule
            {
                Id = id,
                Uid = $"rule-{id}",
                RulebaseId = 3,
                Metadata = new RuleMetadata
                {
                    Id = id * 10,
                    Recert = false
                }
            };
        }

        private sealed class RecordingRecertApiConnection : SimulatedApiConnection
        {
            public List<(string Query, object? Variables)> Queries { get; } = new();
            public List<OwnerRecertification> OwnerRecerts { get; } = new();

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                Queries.Add((query, variables));

                if (query == RecertQueries.getOwnerRecert && typeof(QueryResponseType) == typeof(List<OwnerRecertification>))
                {
                    return Task.FromResult((QueryResponseType)(object)OwnerRecerts);
                }

                if (query == RecertQueries.recertify && typeof(QueryResponseType) == typeof(ReturnId))
                {
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = 1 });
                }

                if (query == RecertQueries.recertifyOwner && typeof(QueryResponseType) == typeof(ReturnIdWrapper))
                {
                    return Task.FromResult((QueryResponseType)(object)CreateWrapper(1234));
                }

                if (query == RecertQueries.recertifyRuleDirectly && typeof(QueryResponseType) == typeof(ReturnIdWrapper))
                {
                    return Task.FromResult((QueryResponseType)(object)CreateWrapper(1));
                }

                if (query == RecertQueries.prepareNextRecertification)
                {
                    return Task.FromResult(default(QueryResponseType)!);
                }

                if (query == OwnerQueries.setOwnerLastRecert && typeof(QueryResponseType) == typeof(ReturnId))
                {
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { UpdatedId = 7 });
                }

                throw new InvalidOperationException($"Unexpected query: {query}");
            }

            public int CountQuery(string query)
            {
                return Queries.Count(item => item.Query == query);
            }

            private static ReturnIdWrapper CreateWrapper(long newIdLong)
            {
                ReturnId[] returnIds = new ReturnId[1];
                returnIds[0] = new ReturnId { NewIdLong = newIdLong };
                return new ReturnIdWrapper { ReturnIds = returnIds };
            }
        }
    }
}
