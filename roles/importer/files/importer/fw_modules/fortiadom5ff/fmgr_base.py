from typing import Any
from services.service_provider import ServiceProvider
from fwo_log import FWOLogger

#TODO: unused functions - remove?
def set_alerts_for_missing_objects(objects_not_found: list[str], import_id: int, rule_uid: str | None, object_type: str | None, mgm_id: int):
    for obj in objects_not_found:
        if obj == 'all' or obj == 'Original':
            continue

        service_provider = ServiceProvider()
        global_state = service_provider.get_global_state()

        api_call = global_state.import_state.api_call

        api_call.create_data_issue(obj_name=obj, severity=1, 
                                    rule_uid=rule_uid, mgm_id=mgm_id, object_type=object_type)

        desc = "found a broken network object reference '" + obj + "' "
        if object_type is not None:
            desc +=  "(type=" + object_type + ") "
        desc += "in rule with UID '" + str(rule_uid) + "'"
        api_call.set_alert(import_id=import_id, title="object reference error", mgm_id=mgm_id, severity=1, 
                    description=desc, source='import', alert_code=16)


def lookup_obj_in_tables(el: str, object_tables: list[list[dict[str, Any]]], name_key: str, uid_key: str, ref_list: list[str]) -> bool:
    break_flag = False 
    found = False

    for tab in object_tables:
        if break_flag:
            found = True
            break
        for obj in tab:
            if obj[name_key] == el:
                if uid_key in obj:
                    ref_list.append(obj[uid_key])
                # in case of internet-service-object we find no uid field, but custom q_origin_key_
                elif 'q_origin_key' in obj:
                    ref_list.append('q_origin_key_' + str(obj['q_origin_key']))
                else:
                    FWOLogger.error('found object without expected uid')
                break_flag = True
                found = True
                break
    return found
