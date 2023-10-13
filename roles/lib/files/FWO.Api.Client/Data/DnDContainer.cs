namespace FWO.Api.Data
{
    public class DnDContainer
    {
        public List<NetworkObject> IpElements { get; set; } = new();
        public List<AppRole> GrpElements { get; set; } = new();
        public List<NetworkService> SvcElements { get; set; } = new();

        public void Clear()
        {
            IpElements = new();
            GrpElements = new();
            SvcElements = new();
        }
    }
}
