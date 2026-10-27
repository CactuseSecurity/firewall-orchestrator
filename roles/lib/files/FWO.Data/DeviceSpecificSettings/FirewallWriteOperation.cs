namespace FWO.Data.DeviceSpecificSettings;

/// <summary>
/// One kind of REST call that writes a change from an implementation task to a firewall. Each operation has its own
/// endpoint and body template setting per device type.
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
