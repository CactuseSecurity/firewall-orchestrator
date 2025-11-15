from typing import Any
from fwo_const import list_delimiter

def normalize_users(full_config: dict[str, list[dict[str, Any]]], config2import: dict[str, list[dict[str, Any]]], import_id: int, user_scope: list[str]) -> None:
    users: list[dict[str, Any]] = []
    for scope in user_scope:
        for user_orig in full_config[scope]:
            user_normalized = _parse_user(user_orig)
            users.append(user_normalized)            

    config2import.update({'user_objects': users})


def _parse_user(user_orig: dict[str, Any]) -> dict[str, Any]:
    name = None
    svc_type = 'simple'
    color = None
    member_names = None
    comment = None
    user: dict[str, Any] = {}
    if 'member' in user_orig:
        svc_type = 'group'
        member_names = ''
        for member in user_orig['member']:
            member_names += member + list_delimiter
        member_names = member_names[:-1]
    if 'name' in user_orig:
        name = str(user_orig['name'])
    if 'comment' in user_orig:
        comment = str(user_orig['comment'])
    if 'color' in user_orig and str(user_orig['color']) != "0":
        color = str(user_orig['color'])

    user.update({  'user_typ': svc_type,
                    'user_name': name, 
                    'user_color': color,
                    'user_uid': name, 
                    'user_comment': comment,
                    'user_member_refs': member_names,
                    'user_member_names': member_names
                })

    return user