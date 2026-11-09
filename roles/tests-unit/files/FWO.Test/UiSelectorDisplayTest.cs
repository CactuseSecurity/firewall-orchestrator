using FWO.Data;
using FWO.Data.Workflow;
using FWO.Ui.Shared;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test;

[TestFixture]
internal class UiSelectorDisplayTest
{
    [Test]
    public void IpSelector_DisplaysNamesIdsAndAddressFallbacks()
    {
        IpSelector selector = new();
        SetMember(selector, nameof(IpSelector.IgnoreGroupName), true);

        Assert.Multiple(() =>
        {
            Assert.That(DisplayIp(selector, new NwObjectElement { Name = "named" }), Is.EqualTo("named"));
            Assert.That(DisplayIp(selector, new NwObjectElement { FlowNetworkObjectId = 41 }), Is.EqualTo("41"));
            Assert.That(DisplayIp(selector, new NwObjectElement { FlowNetworkGroupId = 42 }), Is.EqualTo("42"));
            Assert.That(DisplayIp(selector, new NwObjectElement { NetworkId = 43 }), Is.EqualTo("43"));
            Assert.That(DisplayIp(selector, new NwObjectElement { IpString = "10.0.0.1", IpEndString = "10.0.0.2" }),
                Does.Contain("10.0.0.1").And.Contain("10.0.0.2"));
            Assert.That(DisplayIp(selector, new NwObjectElement { GroupName = "group" }), Is.EqualTo("group"));
        });
    }

    [Test]
    public void ServiceSelector_DisplaysNamesIdsAndServiceFallbacks()
    {
        ServiceSelector selector = new();
        SetMember(selector, nameof(ServiceSelector.IgnoreGroupName), true);
        SetMember(selector, nameof(ServiceSelector.IpProtos), new List<IpProtocol> { new() { Id = 6, Name = "TCP" } });

        Assert.Multiple(() =>
        {
            Assert.That(DisplayService(selector, new NwServiceElement { Name = "named" }), Is.EqualTo("named"));
            Assert.That(DisplayService(selector, new NwServiceElement { FlowServiceObjectId = 51 }), Is.EqualTo("51"));
            Assert.That(DisplayService(selector, new NwServiceElement { FlowServiceGroupId = 52 }), Is.EqualTo("52"));
            Assert.That(DisplayService(selector, new NwServiceElement { ServiceId = 53 }), Is.EqualTo("53"));
            Assert.That(DisplayService(selector, new NwServiceElement { Port = 443, ProtoId = 6 }), Is.EqualTo("443/TCP"));
            Assert.That(DisplayService(selector, new NwServiceElement { GroupName = "group" }), Is.EqualTo("group"));
        });
    }

    private static string DisplayIp(IpSelector selector, NwObjectElement element)
    {
        return InvokePrivate<string>(selector, "DisplayIpAddress", element);
    }

    private static string DisplayService(ServiceSelector selector, NwServiceElement element)
    {
        return InvokePrivate<string>(selector, "DisplayServiceElement", element);
    }

    private static void SetMember(object instance, string memberName, object? value)
    {
        PropertyInfo? property = instance.GetType().GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (property != null)
        {
            property.SetValue(instance, value);
            return;
        }
        FieldInfo? field = instance.GetType().GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(instance, value);
            return;
        }
        throw new MissingMemberException(instance.GetType().FullName, memberName);
    }

    private static T InvokePrivate<T>(object instance, string methodName, params object?[] parameters)
    {
        MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"{methodName} not found.");
        return (T)method.Invoke(instance, parameters)!;
    }
}
