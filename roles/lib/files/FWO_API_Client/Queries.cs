using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FWO.ApiClient
{
    public static class Queries
    {

public static readonly string Rules =
@"
query ($management_id: [Int!], $device_id: [Int!], $rule_src_name: [String!], $rule_src_ip: [cidr!], $limit: Int, $offset: Int) {
  management(where: {mgm_id: {_in: $management_id}}) {
    mgm_id
    mgm_name
    devices(where: {dev_id: {_in: $device_id}}) {
      dev_id
      dev_name
      rules(limit: $limit, offset: $offset, where: {active: {_eq: true}, rule_src: {_in: $rule_src_name}, rule_disabled: {_eq: false}, rule_froms: {object: {obj_ip: {_in: $rule_src_ip}}}}) {
        rule_uid
        rule_num_numeric
        rule_name
        rule_disabled
        rule_src_neg
        rule_src
        rule_froms {
          object {
            obj_ip
            obj_name
          }
        }
        rule_dst_neg
        rule_dst
        rule_tos {
          object {
            obj_ip
            obj_name
          }
        }
        rule_svc_neg
        rule_svc
        rule_services {
          service {
            svc_name
            svc_port
            stm_svc_typ {
              svc_typ_name
            }
          }
        }
        rule_action
        rule_track
        rule_comment
      }
    }
  }
}
";

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
