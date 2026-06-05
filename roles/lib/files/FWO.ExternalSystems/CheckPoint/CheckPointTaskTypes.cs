using FWO.Data.Workflow;

namespace FWO.ExternalSystems.CheckPoint
{
    public class CheckPointInstallPolicyTarget
    {
        public string PolicyPackage { get; set; } = "";
        public List<string> Targets { get; set; } = [];
    }

    public static class CheckPointTaskTypes
    {
        public const string Publish = "publish";
        public const string InstallPolicy = "install_policy";
        public const string GroupCreate = nameof(WfTaskType.group_create);
        public const string GroupModify = nameof(WfTaskType.group_modify);
        public const string GroupDelete = nameof(WfTaskType.group_delete);

        public const string HostCreate = "host_create";
        public const string NetworkCreate = "network_create";
        public const string AddressRangeCreate = "address_range_create";

        public const string HostModify = "host_modify";
        public const string NetworkModify = "network_modify";
        public const string AddressRangeModify = "address_range_modify";
    }
}
