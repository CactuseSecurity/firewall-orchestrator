namespace FWO.Middleware.RequestParameters
{
    public class UserGetReturnParameters
    {
        public string Name { get; set; } = "";
        public int UserId { get; set; }
        public string UserDn { get; set; } = "";
        public string? Email { get; set; }
        public string? Firstname { get; set; }
        public string? Lastname { get; set; }
        public int TenantId { get; set; }
        public string? Language { get; set; }
        public DateTime? LastLogin { get; set; }
        public DateTime? LastPasswordChange { get; set; }
        public bool PwChangeRequired { get; set; }
        public int LdapId { get; set; }
    }

    public class LdapUserGetParameters
    {
        public int LdapId { get; set; }
        public string SearchPattern { get; set; } = "";
    }

    public class LdapUserGetReturnParameters
    {
        public string UserDn { get; set; } = "";
        public string? Email { get; set; }
        public string? Firstname { get; set; }
        public string? Lastname { get; set; }
    }

    public class UserAddParameters
    {
        public int LdapId { get; set; }
        public string UserDn { get; set; } = "";
        public string Password { get; set; } = "";
        public string? Email { get; set; }
        public string? Firstname { get; set; }
        public string? Lastname { get; set; }
        public int TenantId { get; set; }
        public bool PwChangeRequired { get; set; }
    }

    public class UserEditParameters
    {
        public int LdapId { get; set; }
        public int UserId { get; set; }
        public string? Email { get; set; }
        public string? Firstname { get; set; }
        public string? Lastname { get; set; }
    }

    public class UserChangePasswordParameters
    {
        public int LdapId { get; set; }
        public int UserId { get; set; }
        public string OldPassword { get; set; } = "";
        public string NewPassword { get; set; } = "";
    }

    public class UserResetPasswordParameters
    {
        public int LdapId { get; set; }
        public int UserId { get; set; }
        public string NewPassword { get; set; } = "";
    }

    public class UserDeleteParameters
    {
        public int LdapId { get; set; }
        public int UserId { get; set; }
    }

    public class UserDeleteAllEntriesParameters
    {
        public int UserId { get; set; }
    }
}
