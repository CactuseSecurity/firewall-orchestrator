details_level = "standard"
details_level_objects = "standard"
details_level_group_objects = "full"
use_object_dictionary = True
with_hits = True

# the following is the static across all installations unique any obj uid 
# cannot fetch the Any object via API (<=1.7) at the moment
# therefore we have a workaround adding the object manually (as svc and nw)
any_obj_uid = "97aeb369-9aea-11d5-bd16-0090272ccb30"
# todo: read this from config (from API 1.6 on it is fetched)

original_obj_uid = "85c0f50f-6d8a-4528-88ab-5fb11d8fe16c"
# used for nat only (both svc and nw obj)

nw_obj_table_names = [
    'hosts', 'networks', 'groups', 'address-ranges', 'multicast-address-ranges', 'groups-with-exclusion', 
    'gateways-and-servers', 'simple-gateways', 
    'dns-domains', 'updatable-objects-repository-content',
    'interoperable-devices', 'security-zones', 'Global', 'access-roles', 'updatable-objects'
]

# simple as in: no groups
simple_svc_obj_types = ['services-tcp', 'services-udp', 'services-dce-rpc', 'services-rpc', 'services-other', 
                        'services-icmp', 'services-icmp6', 'services-sctp', 'services-gtp', 'Global']
group_svc_obj_types = ['service-groups', 'application-site-categories', 'application-sites']

svc_obj_table_names = group_svc_obj_types + simple_svc_obj_types + [ 'CpmiAnyObject' ]
# usr_obj_table_names : do not exist yet - not fetchable via API

api_obj_types = nw_obj_table_names + svc_obj_table_names  # all obj table names to look at during import

obj_group_types = ['groups', 'groups-with-exclusion', 'updatable-objects'] + group_svc_obj_types

cp_specific_object_types = [    # used for fetching enrichment data via "get object" separately (no specific API call)
    'simple-gateway', 'simple-cluster', 'CpmiVsClusterNetobj', 'CpmiVsxClusterNetobj', 'CpmiVsxClusterMember', 'CpmiVsNetobj',
    'CpmiAnyObject', 'CpmiVsxNetobj', 'CpmiClusterMember', 'CpmiGatewayPlain', 'CpmiHostCkp', 'CpmiGatewayCluster', 'checkpoint-host', 
    'cluster-member', 'CpmiVoipSipDomain'
]
