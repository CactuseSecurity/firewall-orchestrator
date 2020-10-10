using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FWO.ApiClient.Queries
{
    public static class BasicQueries
    {

public static readonly string getTenantId = @"
   query getTenantId($tenant_name: String!) { tenant(where: {tenant_name: {_eq: $tenant_name}}) { tenant_id } }
";

public static readonly string getVisibleDeviceIdsPerTenant = @"
   query getVisibleDeviceIdsPerTenant($tenantId: Int!) { visibleDevices: get_visible_devices_per_tenant(args: {arg_1: $tenantId}) { id } }
";

public static readonly string getVisibleManagementIdsPerTenant = @"
   query getVisibleManagementIdsPerTenant($tenantId: Int!) { visibleManagements: get_visible_managements_per_tenant(args: {arg_1: $tenantId}) { id } }
";

public static readonly string LdapConnections = @"
   query getLdapConnections
   {
     ldap_connection
      { 
        ldap_server 
        ldap_port 
        ldap_search_user 
        ldap_tls 
        ldap_tenant_level 
        ldap_connection_id 
        ldap_search_user_pwd 
        ldap_searchpath_for_users
        ldap_searchpath_for_roles
      } 
    }
";
    }
}
