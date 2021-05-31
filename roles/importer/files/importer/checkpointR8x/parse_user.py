import logging
import common


def csv_dump_user(user_name, user, import_id):
    user_line = '"' + import_id + '"' + common.csv_delimiter
    user_line += '"' + user_name + '"' + common.csv_delimiter
    user_line += '"' + user['user_type'] + '"' + common.csv_delimiter  # user_typ
    if 'user_member_names' in user:
        user_line += '"' + user['user_member_names'] + '"' + common.csv_delimiter  # user_member_names
    else:
        user_line += common.csv_delimiter  # user_comment
    if 'user_member_refs' in user:
        user_line += '"' + user['user_member_refs'] + '"' + common.csv_delimiter  # user_member_refs
    else:
        user_line += common.csv_delimiter  # user_comment
    if 'user_color' in user:
        user_line += '"' + user['user_color'] + '"' + common.csv_delimiter  # user_color
    else:
        user_line += common.csv_delimiter  # user_comment
    if 'user_comment' in user:
        user_line += '"' + user['user_comment'] + '"' + common.csv_delimiter  # user_comment
    else:
        user_line += common.csv_delimiter  # user_comment
    user_line += '"' + user['uid'] + '"'  # user_uid
    user_line += common.csv_delimiter  # user_valid_until
    user_line += common.csv_delimiter  # last_change_admin
    user_line += common.line_delimiter
    return user_line


def collect_users_from_rule(rule, users):
    if 'rule-number' in rule:  # standard rule
        if 'type' in rule and rule['type'] != 'place-holder':
            for src in rule["source"]:
                if src['type'] == 'access-role':
                    users.update({src['name']: {'uid': src['uid'], 'user_type': 'group', 'comment': src['comments'], 'color': src['color']} })
                    if 'users' in src:
                        users.update({src["name"]: {'uid': src["uid"], 'user_type': 'simple'} })
                elif src['type'] == 'LegacyUserAtLocation':
                    user_str = src["name"]
                    user_ar = user_str.split('@')
                    user_name = user_ar[0]
                    user_uid = src["userGroup"]
                    users.update({user_name: {'uid': user_uid, 'user_type': 'group'} })
    else:  # section
        collect_users_from_rulebase(rule["rulebase"], users)


# collect_users writes user info into global users dict
def collect_users_from_rulebase(rulebase, users):
    result = ''
    if 'layerchunks' in rulebase:
        for chunk in rulebase['layerchunks']:
            if 'rulebase' in chunk:
                for rule in chunk['rulebase']:
                    collect_users_from_rule(rule, users)
    else:
        for rule in rulebase:
            collect_users_from_rule(rule, users)
