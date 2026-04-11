using FWO.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Ui.Pages.Settings;
using NUnit.Framework;
using System.Collections;
using System.Reflection;
using System.Text.Json;

namespace FWO.Test
{
    [TestFixture]
    internal class UiEditFixCriterionTest
    {
        private static MethodInfo GetPrivateMethod(string name)
        {
            return typeof(EditFixCriterion).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(EditFixCriterion).FullName, name);
        }

        private static T GetPrivateField<T>(EditFixCriterion component, string fieldName)
        {
            FieldInfo? field = typeof(EditFixCriterion).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(typeof(EditFixCriterion).FullName, fieldName);
            }

            return (T)field.GetValue(component)!;
        }

        private static void SetPublicProperty<T>(EditFixCriterion component, string propertyName, T value)
        {
            PropertyInfo? property = typeof(EditFixCriterion).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property == null)
            {
                throw new MissingMemberException(typeof(EditFixCriterion).FullName, propertyName);
            }

            property.SetValue(component, value);
        }

        private static void SetComponentMember<T>(EditFixCriterion component, string memberName, T value)
        {
            PropertyInfo? property = typeof(EditFixCriterion).GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
            {
                property.SetValue(component, value);
                return;
            }

            FieldInfo? field = typeof(EditFixCriterion).GetField(memberName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(component, value);
                return;
            }

            throw new MissingMemberException(typeof(EditFixCriterion).FullName, memberName);
        }

        [Test]
        public void OnParametersSet_ForbiddenServiceWithRemovedConditionsAndNullContent_LoadsEmptyElements()
        {
            EditFixCriterion component = new();
            SetPublicProperty(component, nameof(EditFixCriterion.Display), true);
            SetPublicProperty(component, nameof(EditFixCriterion.SelectedCriterion), new ComplianceCriterion
            {
                CriterionType = nameof(CriterionType.ForbiddenService),
                Content = null!,
                Conditions =
                [
                    new ComplianceCriterionCondition
                    {
                        GroupOrder = 1,
                        Field = ComplianceConditionFields.ServiceUid,
                        ValueString = "legacy-service",
                        Removed = DateTime.UtcNow
                    }
                ]
            });

            GetPrivateMethod("OnParametersSet").Invoke(component, null);

            Assert.That(component.SelectedCriterion.Content, Is.EqualTo(""));
            Assert.That(GetPrivateField<List<string>>(component, "ActElements"), Is.Empty);
        }

        [Test]
        public void OnParametersSet_ForbiddenServiceWithActiveConditions_PrefersConditionFormatting()
        {
            EditFixCriterion component = new();
            SetPublicProperty(component, nameof(EditFixCriterion.Display), true);
            SetPublicProperty(component, nameof(EditFixCriterion.SelectedCriterion), new ComplianceCriterion
            {
                CriterionType = nameof(CriterionType.ForbiddenService),
                Content = null!,
                Conditions =
                [
                    new ComplianceCriterionCondition
                    {
                        GroupOrder = 1,
                        Field = ComplianceConditionFields.ServiceUid,
                        ValueString = "service-a"
                    }
                ]
            });

            GetPrivateMethod("OnParametersSet").Invoke(component, null);

            Assert.That(GetPrivateField<List<string>>(component, "ActElements"), Is.EqualTo(new List<string> { "service-a" }));
        }

        [Test]
        public async Task Save_AddModeForbiddenServicePortRange_SendsNestedPortCondition()
        {
            EditFixCriterionTestApiConn apiConnection = new();
            EditFixCriterion component = new();
            ComplianceCriterion criterion = new()
            {
                Name = "forbidden port range",
                CriterionType = nameof(CriterionType.ForbiddenService)
            };

            SetPublicProperty(component, nameof(EditFixCriterion.Display), true);
            SetPublicProperty(component, nameof(EditFixCriterion.AddMode), true);
            SetPublicProperty(component, nameof(EditFixCriterion.CriteriaList), new List<ComplianceCriterion>());
            SetPublicProperty(component, nameof(EditFixCriterion.SelectedCriterion), criterion);
            SetComponentMember(component, "apiConnection", apiConnection);
            SetPrivateField(component, "IpProtocols", new List<IpProtocol>());
            SetPrivateField(component, "ActForbiddenPortStart", 1000);
            SetPrivateField(component, "ActForbiddenPortEnd", 2000);

            GetPrivateMethod("AddForbiddenCondition").Invoke(component, null);

            Task saveTask = (Task)GetPrivateMethod("Save").Invoke(component, null)!;
            await saveTask;

            Assert.That(apiConnection.LastAddCriterionVariables, Is.Not.Null);

            JsonElement root = JsonSerializer.SerializeToElement(apiConnection.LastAddCriterionVariables);
            JsonElement conditions = JsonSerializer.SerializeToElement(apiConnection.LastAddCriterionConditionsVariables).GetProperty("conditions");

            Assert.That(conditions.GetArrayLength(), Is.EqualTo(1));
            Assert.That(root.GetProperty("content").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(root.TryGetProperty("conditions", out _), Is.False);

            JsonElement portCondition = conditions[0];
            Assert.That(portCondition.GetProperty("criterion_id").GetInt32(), Is.EqualTo(101));
            Assert.That(portCondition.GetProperty("field").GetString(), Is.EqualTo(ComplianceConditionFields.Port));
            Assert.That(portCondition.GetProperty("operator").GetString(), Is.EqualTo(ComplianceConditionOperators.Overlaps));
            Assert.That(portCondition.GetProperty("value_int").GetInt32(), Is.EqualTo(1000));
            Assert.That(portCondition.GetProperty("value_int_end").GetInt32(), Is.EqualTo(2000));
        }

        [Test]
        public async Task Save_AddModeForbiddenServicePendingPortRange_SendsNestedPortCondition()
        {
            EditFixCriterionTestApiConn apiConnection = new();
            EditFixCriterion component = new();
            ComplianceCriterion criterion = new()
            {
                Name = "pending forbidden port range",
                CriterionType = nameof(CriterionType.ForbiddenService)
            };

            SetPublicProperty(component, nameof(EditFixCriterion.Display), true);
            SetPublicProperty(component, nameof(EditFixCriterion.AddMode), true);
            SetPublicProperty(component, nameof(EditFixCriterion.CriteriaList), new List<ComplianceCriterion>());
            SetPublicProperty(component, nameof(EditFixCriterion.SelectedCriterion), criterion);
            SetComponentMember(component, "apiConnection", apiConnection);
            SetPrivateField(component, "IpProtocols", new List<IpProtocol>());
            SetPrivateField(component, "ActForbiddenPortStart", 8080);
            SetPrivateField(component, "ActForbiddenPortEnd", 8090);

            Task saveTask = (Task)GetPrivateMethod("Save").Invoke(component, null)!;
            await saveTask;

            Assert.That(apiConnection.LastAddCriterionVariables, Is.Not.Null);

            JsonElement conditions = JsonSerializer.SerializeToElement(apiConnection.LastAddCriterionConditionsVariables).GetProperty("conditions");

            Assert.That(conditions.GetArrayLength(), Is.EqualTo(1));
            Assert.That(conditions[0].GetProperty("criterion_id").GetInt32(), Is.EqualTo(101));
            Assert.That(conditions[0].GetProperty("field").GetString(), Is.EqualTo(ComplianceConditionFields.Port));
            Assert.That(conditions[0].GetProperty("value_int").GetInt32(), Is.EqualTo(8080));
            Assert.That(conditions[0].GetProperty("value_int_end").GetInt32(), Is.EqualTo(8090));
        }

        [Test]
        public void AddCriterion_WithoutInsertedId_Throws()
        {
            EditFixCriterionTestApiConn apiConnection = new()
            {
                ReturnIds = []
            };
            EditFixCriterion component = new();

            SetPublicProperty(component, nameof(EditFixCriterion.SelectedCriterion), new ComplianceCriterion
            {
                Name = "crit",
                CriterionType = nameof(CriterionType.ForbiddenService)
            });
            SetComponentMember(component, "apiConnection", apiConnection);

            Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                Task addTask = (Task)GetPrivateMethod("AddCriterion").Invoke(component, null)!;
                await addTask;
            });
        }

        [Test]
        public async Task Save_EditMode_AddsReplacementBeforeRemovingOldCriterion()
        {
            EditFixCriterionTestApiConn apiConnection = new();
            EditFixCriterion component = new();
            ComplianceCriterion criterion = new()
            {
                Id = 7,
                Name = "existing criterion",
                CriterionType = nameof(CriterionType.ForbiddenService)
            };

            SetPublicProperty(component, nameof(EditFixCriterion.Display), true);
            SetPublicProperty(component, nameof(EditFixCriterion.AddMode), false);
            SetPublicProperty(component, nameof(EditFixCriterion.CriteriaList), new List<ComplianceCriterion> { criterion });
            SetPublicProperty(component, nameof(EditFixCriterion.SelectedCriterion), criterion);
            SetComponentMember(component, "apiConnection", apiConnection);
            SetPrivateField(component, "IpProtocols", new List<IpProtocol>());
            SetPrivateField(component, "ActForbiddenPortStart", 9000);
            SetPrivateField(component, "ActForbiddenPortEnd", 9010);

            Task saveTask = (Task)GetPrivateMethod("Save").Invoke(component, null)!;
            await saveTask;

            Assert.That(apiConnection.QuerySequence, Is.EqualTo(new[]
            {
                ComplianceQueries.addCriterion,
                ComplianceQueries.addCriterionConditions,
                ComplianceQueries.getPolicyIdsForCrit,
                ComplianceQueries.removeCriterion
            }));
            Assert.That(criterion.Id, Is.EqualTo(101));
        }

        private static void SetPrivateField<T>(EditFixCriterion component, string fieldName, T value)
        {
            FieldInfo? field = typeof(EditFixCriterion).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(typeof(EditFixCriterion).FullName, fieldName);
            }

            field.SetValue(component, value);
        }
    }

    internal sealed class EditFixCriterionTestApiConn : SimulatedApiConnection
    {
        public object? LastAddCriterionVariables { get; private set; }
        public object? LastAddCriterionConditionsVariables { get; private set; }
        public List<string> QuerySequence { get; } = [];
        public ReturnId[] ReturnIds { get; set; } = [new ReturnId { InsertedId = 101 }];

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
        {
            QuerySequence.Add(query);

            if (query == ComplianceQueries.addCriterion && typeof(QueryResponseType) == typeof(ReturnIdWrapper))
            {
                LastAddCriterionVariables = variables;
                return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper
                {
                    ReturnIds = ReturnIds
                });
            }

            if (query == ComplianceQueries.addCriterionConditions && typeof(QueryResponseType) == typeof(ReturnIdWrapper))
            {
                LastAddCriterionConditionsVariables = variables;
                JsonElement root = JsonSerializer.SerializeToElement(variables);
                int conditionCount = root.GetProperty("conditions").GetArrayLength();
                return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper
                {
                    ReturnIds = Enumerable.Range(1, conditionCount).Select(id => new ReturnId { InsertedId = id }).ToArray()
                });
            }

            if (query == ComplianceQueries.getPolicyIdsForCrit && typeof(QueryResponseType) == typeof(List<LinkedPolicy>))
            {
                return Task.FromResult((QueryResponseType)(object)new List<LinkedPolicy>());
            }

            if (typeof(QueryResponseType) == typeof(ReturnId))
            {
                return Task.FromResult((QueryResponseType)(object)new ReturnId());
            }

            throw new NotImplementedException($"Unhandled query in test connection: {query}");
        }
    }
}
