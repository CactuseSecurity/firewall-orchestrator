using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FWO.ApiClient
{
    public static class Queries
    {

public static readonly string ServiceObjects =
@"
query listServiceObjectDetails($svcTyp: String, $svcName: String, $svcPort: Int, $svcUid: String) {
            service(where: {stm_svc_typ: {svc_typ_name: {_eq: $svcTyp}}, svc_name: {_like: $svcName}, svc_port: {_eq: $svcPort}, svc_uid: {_eq: $svcUid}}) {
              svc_uid
              svc_name
              svc_typ_id
              stm_svc_typ {
                svc_typ_name
              }
              ip_proto_id
              svc_comment
              svc_member_names
              svc_member_refs
              svc_port
              svc_port_end
              svc_code
              svcgrpsBySvcgrpMemberId {
                svcgrp_member_id
                service {
                  svc_id
                  svc_uid
                  svc_name
                }
              }
            }
          }
";

public static readonly string NetworkObjects =
@"
query showNetworkObjectDetails($nwObjTyp: String, $nwObjName: String, $nwObjIp: cidr, $nwObjUid: String) {
  object(where: {stm_obj_typ: {obj_typ_name: {_eq: $nwObjTyp}}, obj_name: {_like: $nwObjName}, obj_ip: {_eq: $nwObjIp}, obj_uid: {_eq: $nwObjUid}}) {
    obj_name
    obj_ip
    obj_ip_end
    obj_uid
    zone_id
    stm_obj_typ {
      obj_typ_name
    }
    obj_comment
    obj_member_names
    obj_member_refs
    objgrps {
      objgrp_member_id
      objectByObjgrpMemberId {
        obj_id
        obj_name
      }
    }
    objgrp_flats {
      objgrp_flat_id
      objectByObjgrpFlatMemberId {
        obj_id
        obj_name
      }
    }
  }
}
";


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

public static readonly string getTenantId = @"
   query getTenantId($tenant_name: String) { tenant(where: {tenant_name: {_eq: $tenant_name}}) { tenant_id } }
";
// variables: {"tenant_name": "forti"}

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
