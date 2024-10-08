from fwo_const import list_delimiter
from fwo_log import getFwoLogger


def normalize_svcobjects(full_config, config2import, import_id):
    svc_objects = []
    for svc_orig in full_config["/Objects/Services"]:
        svc_objects.append(parse_svc(svc_orig, import_id,config2import))
    for svc_grp_orig in full_config["/Objects/ServiceGroups"]:
        svc_grp = extract_base_svc_infos(svc_grp_orig, import_id)
        svc_grp["svc_typ"] = "group"
        svc_grp["svc_member_refs"] , svc_grp["svc_member_names"] = parse_svc_group(svc_grp_orig,config2import)
        svc_objects.append(svc_grp)
    config2import['service_objects'] += svc_objects


def parse_svc_group(orig_grp,config2import):
    refs = []
    names = []
    if "dynamic" in orig_grp:
        pass
    if "static" in orig_grp and "member" in orig_grp["static"]:
        for m in orig_grp['static']['member']:
            names.append(m)
            refs.append(m)
    return list_delimiter.join(refs), list_delimiter.join(names)

    
def extract_base_svc_infos(svc_orig, import_id):
    svc = {}
    if "@name" in svc_orig:
        svc["svc_uid"] = svc_orig["@name"]
        svc["svc_name"] = svc_orig["@name"]
    if "comment" in svc_orig:
        svc["svc_comment"] = svc_orig["comment"]
    svc["svc_timeout"] = None
    svc["svc_color"] = None
    svc["control_id"] = import_id 
    svc["svc_typ"] = 'simple' 
    return svc


def parse_svc(svc_orig, import_id,config2import):
    svc = extract_base_svc_infos(svc_orig, import_id)
    if 'protocol' in svc_orig:
        proto_string = 'undefined'
        if 'tcp' in svc_orig['protocol']:
            svc["ip_proto"] = 6
            proto_string = 'tcp'
            svc["svc_port"] = svc_orig['protocol']['tcp']['port']
        elif 'udp' in svc_orig['protocol']:
            svc["ip_proto"] = 17
            proto_string = 'udp'
            
        if proto_string=='undefined':
            svc["svc_name"] += " [Protocol \"" + str(svc_orig["protocol"]) + "\" not supported]"
        else:
            port_string = svc_orig['protocol'][proto_string]['port']
            if ',' in port_string:
                svc["svc_typ"] = "group"
                svc["svc_port"] = None
                members = []
                for p in port_string.split(","):
                    hlp_svc = create_helper_service(p, proto_string, svc["svc_name"], import_id)
                    add_service(hlp_svc, config2import)
                    members.append(hlp_svc['svc_uid'])
                svc["svc_members"] = list_delimiter.join(members)                
                svc["svc_member_refs"] = list_delimiter.join(members)                
            else:   # just a single port (range)
                extract_port_for_service(port_string, svc)
    return svc


def add_service(svc, config2import):
    if 'service_objects' not in config2import:
        config2import.update({'service_objects': []})
    config2import['service_objects'].append(svc)


def extract_port_for_service(port_string, svc):
    if '-' in port_string:
        port_range = port_string.split("-")
        if len(port_range)==2:
            svc["svc_port"] = port_range[0]
            svc["svc_port_end"] = port_range[1]
        else:
            logger = getFwoLogger()
            logger.warning("found strange port range with more than one hyphen: " + str(port_string))
    else:
        svc["svc_port"] = port_string


def create_helper_service(ports, proto_string, parent_svc_name, import_id):
    svc = {
        "svc_name": parent_svc_name + "_" + proto_string + "_" + ports,
        "svc_uid": parent_svc_name + "_" + proto_string + "_" + ports,
        "svc_comment": "helper service for Palo Alto multiple port range object: " + parent_svc_name,
        "control_id": import_id, 
        "svc_typ": 'simple' 
    }

    extract_port_for_service(ports, svc)    
    return svc


def parse_svc_list(nw_obj_list, import_id, obj_list, id, type='network'):
    refs = []
    names = []
    for obj_name in nw_obj_list:
        names.append(obj_name)
        refs.append(lookup_svc_obj_uid(obj_name, obj_list, import_id, type=type))
    return list_delimiter.join(refs), list_delimiter.join(names)


def lookup_svc_obj_uid(obj_name, obj_list, import_id, type='network'):
    logger = getFwoLogger()
    for o in obj_list:
        if type=='service' and 'svc_name' in o:
            if o['svc_name']==obj_name:
                return o['svc_uid']
        else:
            logger.warning("could not find object name in object " + str(o))

    # could not find existing obj in obj list, so creating new one
    return add_svc_obj(obj_name, obj_list, import_id)


def add_svc_obj(svc_in, svc_list, import_id):
    svc_obj = {}
    svc_obj['svc_name'] = "#" + svc_in
    svc_obj['svc_uid'] = "#" + svc_in
    svc_obj['control_id'] = import_id
    svc_obj['svc_typ'] = 'simple'

    svc_list.append(svc_obj)
    return svc_obj['svc_name']
