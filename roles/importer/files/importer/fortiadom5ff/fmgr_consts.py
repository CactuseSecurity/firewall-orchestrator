# delete_v: brauchen scope nicht mehr?
#scope = ['global', 'adom']
nw_obj_types = ['firewall/address', 'firewall/address6', 'firewall/addrgrp',
                'firewall/addrgrp6', 'firewall/ippool', 'firewall/vip', 'system/external-resource']
svc_obj_types = ['application/list', 'application/group', 'application/categories',
                 'application/custom', 'firewall/service/custom', 'firewall/service/group']

v4_object_types = ['nw_obj_global_firewall/address', 'nw_obj_global_firewall/addrgrp']

v6_object_types = ['nw_obj_adom_firewall/address', 'nw_obj_adom_firewall/addrgrp','nw_obj_global_firewall/address', \
                   'nw_obj_global_firewall/addrgrp', 'nw_obj_adom_firewall/vip', 'nw_obj_adom_system/external-resource']
                
# build the product of all scope/type combinations
#nw_obj_scope = ['nw_obj_' + s1 + '_' +
#                s2 for s1 in scope for s2 in nw_obj_types]
#svc_obj_scope = ['svc_obj_' + s1 + '_' +
#                 s2 for s1 in scope for s2 in svc_obj_types]

# zone_types = ['zones_global', 'zones_adom']

user_obj_types = ['user/local', 'user/group']
#user_scope = ['user_obj_' + s1 + '_' +
#                s2 for s1 in scope for s2 in user_obj_types]
