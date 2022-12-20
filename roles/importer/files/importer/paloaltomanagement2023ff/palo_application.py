from fwo_const import list_delimiter
from fwo_log import getFwoLogger


def normalize_application_objects(full_config, config2import, import_id):
    app_objects = []
    for app_orig in full_config["/Objects/Applications"]:
        app_objects.append(parse_app(app_orig, import_id,config2import))
    config2import['service_objects'] += app_objects


def extract_base_app_infos(app_orig, import_id):
    app = {}
    if "@name" in app_orig:
        app["svc_uid"] = app_orig["@name"]
        app["svc_name"] = app_orig["@name"]
    if "comment" in app_orig:
        app["svc_comment"] = app_orig["comment"]
    app["control_id"] = import_id 
    app["svc_typ"] = 'simple' 
    return app


def parse_app(app_orig, import_id,config2import):
    svc = extract_base_app_infos(app_orig, import_id)
    app_comment = ''
    if 'category' in app_orig:
        app_comment = "category: " + app_orig['category']
        if 'subcategory' in app_orig:
            app_comment += ", " +  "subcategory: " + app_orig['subcategory']
            if 'technology' in app_orig:
                app_comment += ", " + "technology: " + app_orig['technology']
    if 'svc_comment' in svc:
        svc['svc_comment'] += "; " + app_comment
    else:
        svc['svc_comment'] = app_comment
    return svc
