namespace FWO.Middleware.RequestParameters
{
    public class TenantAddParameters
    {
        public string Name { get; set; } = "";
        public string? Comment { get; set; }
        public string? Project { get; set; }
        public bool ViewAllDevices { get; set; }
        // public bool Superadmin { get; set; }
    }

    public class TenantGetReturnParameters : TenantAddParameters
    {
        public int Id { get; set; }
        public List<TenantViewDevice> Devices { get; set; } = new List<TenantViewDevice>();
    }

    public class TenantViewDevice
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class TenantEditParameters
    {
        public int Id { get; set; }
        public string? Comment { get; set; }
        public string? Project { get; set; }
        public bool ViewAllDevices { get; set; }
    }

    public class TenantDeleteParameters
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }
}
