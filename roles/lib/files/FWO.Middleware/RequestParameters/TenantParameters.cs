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
        public List<TenantViewGateway> SharedGateways { get; set; } = new List<TenantViewGateway>();
        public List<TenantViewManagement> SharedManagements { get; set; } = new List<TenantViewManagement>();
        public List<TenantViewGateway> UnfilteredGateways { get; set; } = new List<TenantViewGateway>();
        public List<TenantViewManagement> UnfilteredManagements { get; set; } = new List<TenantViewManagement>();
        public List<TenantViewGateway> VisibleGateways { get; set; } = new List<TenantViewGateway>();
    }

    public class TenantViewGateway
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public bool shared { get; set; } = true;
    }
    public class TenantViewManagement
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public bool shared { get; set; } = true;
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
