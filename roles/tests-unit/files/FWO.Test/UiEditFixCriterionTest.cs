using FWO.Data;
using FWO.Ui.Pages.Settings;
using NUnit.Framework;
using System.Reflection;

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
    }
}
