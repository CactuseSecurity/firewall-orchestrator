using System.Reflection;
using FWO.Config.Api;
using FWO.Data.DeviceSpecificSettings;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Guards the read API of <see cref="DeviceSpecificSettingsManager"/>. Settings must be resolved along the current
/// device hierarchy, which only a <see cref="DeviceSpecificScopePath"/> carries. A read method that accepted a bare
/// <see cref="DeviceSpecificSettingsScope"/> would have to follow the stored parent links, which are outdated for a
/// management whose device type changed or a gateway moved to another management, and would return the settings
/// of the old parent without any error.
/// </summary>
[TestFixture]
[Parallelizable]
internal class DeviceSpecificSettingsManagerApiGuardTest
{
    private static readonly List<Type> kResolvedSettingsTypes = [typeof(DeviceSpecificSettingsLevel<>), typeof(ResolvedDeviceSpecificValue<>)];

    /// <summary>At least LoadLevelAsync and LoadEffectiveValueAsync resolve settings.</summary>
    private const int kMinimumReadMethods = 2;

    [Test]
    public void ReadMethods_TakeAPathAndNoBareScope()
    {
        List<MethodInfo> readMethods = typeof(DeviceSpecificSettingsManager)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(ReturnsResolvedSettings)
            .ToList();

        List<string> violations = readMethods
            .Where(method => !method.GetParameters().Any(parameter => parameter.ParameterType == typeof(DeviceSpecificScopePath))
                || method.GetParameters().Any(parameter => MentionsScope(parameter.ParameterType)))
            .Select(method => method.Name)
            .ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(readMethods, Has.Count.GreaterThanOrEqualTo(kMinimumReadMethods),
                "the guard no longer finds the read methods it is meant to check");
            Assert.That(violations, Is.Empty,
                "read methods must take a DeviceSpecificScopePath and no DeviceSpecificSettingsScope: " + string.Join(", ", violations));
        }
    }

    private static bool ReturnsResolvedSettings(MethodInfo method)
    {
        Type returnType = method.ReturnType;
        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            returnType = returnType.GetGenericArguments()[0];
        }
        return returnType.IsGenericType && kResolvedSettingsTypes.Contains(returnType.GetGenericTypeDefinition());
    }

    /// <summary>True for a scope parameter, also inside a collection or another generic type.</summary>
    private static bool MentionsScope(Type type)
    {
        return type == typeof(DeviceSpecificSettingsScope)
            || (type.HasElementType && MentionsScope(type.GetElementType()!))
            || type.GetGenericArguments().Any(MentionsScope);
    }
}
