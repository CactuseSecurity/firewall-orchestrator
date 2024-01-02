namespace FWO.Middleware.RequestParameters

// used for accessing tenant data stored in LDAP via REST UserManagement API
// but tenant to device mappings (not stored in LDAP but in DB) are also handled here

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
        public List<TenantViewManagement> VisibleManagements { get; set; } = new List<TenantViewManagement>();
    }

    public class TenantViewGateway
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public bool Shared { get; set; } = true;

        public TenantViewGateway (int id, string name = "", bool shared = true)
        {
            Id = id;
            Name = name;
            Shared = shared;
        }
    }
    public class TenantViewManagement
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public bool Shared { get; set; } = true;
        public TenantViewManagement (int id, string name = "", bool shared = true)
        {
            Id = id;
            Name = name;
            Shared = shared;
        }
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
