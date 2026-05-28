using NetTools;
using FWO.Basics;


namespace FWO.ExternalSystems.CheckPoint
{
    internal sealed class CheckPointObjectRequest
    {
        public string NetworkObjectType { get; init; } = ObjectType.Host;
        public string Name { get; init; } = "";
        public IPAddressRange Range { get; init; } = default!;
        public string Comment { get; init; } = "";

        public string StartIp => Range.Begin.ToString();
        public string EndIp => Range.End.ToString();
        public string IpAddress => StartIp;
        public string Subnet => StartIp;
        public int MaskLength => Range.GetPrefixLength();
    }
}
