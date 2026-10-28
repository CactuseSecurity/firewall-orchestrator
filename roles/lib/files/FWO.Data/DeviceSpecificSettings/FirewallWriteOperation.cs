namespace FWO.Data.DeviceSpecificSettings;

/// <summary>
/// One kind of REST call that writes a change from an implementation task to a firewall. Each operation has its own
/// endpoint and body template setting per device type. IPv6 network objects have operations of their own, because some
/// vendors keep them in separate tables and their calls differ.
/// </summary>
public enum FirewallWriteOperation
{
    HostCreate,
    HostModify,
    HostDelete,
    NetworkCreate,
    NetworkModify,
    NetworkDelete,
    AddressRangeCreate,
    AddressRangeModify,
    AddressRangeDelete,
    Ipv6HostCreate,
    Ipv6HostModify,
    Ipv6HostDelete,
    Ipv6NetworkCreate,
    Ipv6NetworkModify,
    Ipv6NetworkDelete,
    Ipv6AddressRangeCreate,
    Ipv6AddressRangeModify,
    Ipv6AddressRangeDelete,
    ServiceCreate,
    ServiceModify,
    ServiceDelete,
    GroupCreate,
    GroupAddMembers,
    GroupRemoveMembers,
    GroupDelete,
    RuleCreate,
    RuleModify,
    RuleDelete,
    Publish,
    InstallPolicy
}
