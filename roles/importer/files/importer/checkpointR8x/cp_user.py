from typing import Any
from fwo_log import FWOLogger
import json

def collect_users_from_rule(rule: dict[str, Any], users: dict[str, Any]): #, objDict):
    if 'rule-number' in rule:  # standard rule
        if 'type' in rule and rule['type'] != 'place-holder':
            for src in rule["source"]:
                # need to get all details for the user first!
                if 'type' in src:
                    if src['type'] == 'access-role' or src['type'] == 'LegacyUserAtLocation':
                        if src['type'] == 'access-role':
                            user_name = src['name']
                            user_uid = src['uid']
                            user_typ = 'group'
                            user_comment = src.get('comments', None)
                            user_color = src.get('color', None)
                            if 'users' in src:
                                user_typ = 'simple'
                        elif src['type'] == 'LegacyUserAtLocation':
                            user_str = src["name"]
                            user_ar = user_str.split('@')
                            user_name = user_ar[0]
                            user_uid = src.get('userGroup', None)
                            user_typ = 'group'
                            user_comment = src.get('comments', None)
                            user_color = src.get('color', None)
                        else:
                            break
                        if user_comment == '':
                            user_comment = None

                        if user_color is None:
                            user_color = 'black'

                        users.update({user_name: {'user_uid': user_uid, 'user_typ': user_typ,
                                     'user_comment': user_comment, 'user_color': user_color}})
                else:
                    FWOLogger.warning("found src user without type field: " + json.dumps(src))
                    if 'name' in src and 'uid' in src:
                        users.update({src["name"]: {'user_uid': src["uid"], 'user_typ': 'simple'}})

    else:  # section
        collect_users_from_rulebase(rule["rulebase"], users)


# collect_users writes user info into global users dict
def collect_users_from_rulebase(rulebase: dict[str, Any], users: dict[str, Any]) -> None:
    if 'rulebase_chunks' in rulebase:
        for chunk in rulebase['rulebase_chunks']:
            if 'rulebase' in chunk:
                for rule in chunk['rulebase']:
                    collect_users_from_rule(rule, users)
    else:
        for rule in rulebase:
            collect_users_from_rule(rule, users) # type: ignore #TODO refactor this 


# the following is only used within new python-only importer:
def parse_user_objects_from_rulebase(rulebase: dict[str, Any], users: dict[str, Any], import_id: str) -> None:
    collect_users_from_rulebase(rulebase, users)
    for user_name in users.keys():
        # TODO: get user info via API
        _ = get_user_uid_from_cp_api(user_name)
        # finally add the import id
        users[user_name]['control_id'] = import_id


def get_user_uid_from_cp_api(userName: str) -> str:
    # show-object with UID
    # dummy implementation returning the name as uid
    return userName


def normalize_users_legacy() -> None:
    raise NotImplementedError
