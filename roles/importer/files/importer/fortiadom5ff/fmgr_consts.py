
nw_obj_types = ['firewall/address', 'firewall/address6', 'firewall/addrgrp',
                'firewall/addrgrp6', 'firewall/ippool', 'firewall/vip', 'system/external-resource',
                'firewall/wildcard-fqdn/custom', 'firewall/wildcard-fqdn/group']

svc_obj_types = ['application/list', 'application/group', 'application/categories',
                 'application/custom', 'firewall/service/custom', 'firewall/service/group']

nat_types = ['central/dnat', 'central/dnat6', 'firewall/central-snat-map']

# delte_v: beide typen können weg
# v4_object_types = ['nw_obj_global_firewall/address', 'nw_obj_global_firewall/addrgrp']
# v6_object_types = ['nw_obj_adom_firewall/address', 'nw_obj_adom_firewall/addrgrp','nw_obj_global_firewall/address', \
#                    'nw_obj_global_firewall/addrgrp', 'nw_obj_adom_firewall/vip', 'nw_obj_adom_system/external-resource']
                
user_obj_types = ['user/local', 'user/group']
