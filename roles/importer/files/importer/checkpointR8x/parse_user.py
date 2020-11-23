import argparse
import json
import re
import logging

csv_delimiter = '%'
list_delimiter = '|'
line_delimiter = "\n"
found_rulebase = False
section_header_uids=[]

# the following is the static across all installations unique any obj uid 
# cannot fetch the Any object via API (<=1.7) at the moment
# therefore we have a workaround adding the object manually (as svc and nw)
any_obj_uid = "97aeb369-9aea-11d5-bd16-0090272ccb30"
# todo: read this from config (vom API 1.6 on it is fetched)


def csv_dump_user(user_name):
    user_dict = users[user_name]
    user_line = '"' + args.import_id + '"' + csv_delimiter
    user_line += '"' + user_name + '"' + csv_delimiter
    user_line += '"' + user_dict['user_type'] + '"' + csv_delimiter  # user_typ
    user_line += csv_delimiter  # user_member_names
    user_line += csv_delimiter  # user_member_refs
    user_line += csv_delimiter  # user_color
    user_line += csv_delimiter  # user_comment
    user_line += '"' + user_dict['uid'] + '"'  # user_uid
    user_line += csv_delimiter  # user_valid_until
    user_line += csv_delimiter  # last_change_admin
    user_line += line_delimiter
    return user_line


def collect_users_from_rule(rule):
    if 'rule-number' in rule:  # standard rule
        for src in rule["source"]:
            if src['type'] == 'access-role':
                users[src['name']] = {'uid': src['uid'], 'user_type': 'group', 'comment': src['comments'],
                                      'color': src['color']}
                if 'users' in src:
                    users[src["name"]] = {'uid': src["uid"], 'user_type': 'simple'}
            elif src['type'] == 'LegacyUserAtLocation':
                user_str = src["name"]
                user_ar = user_str.split('@')
                user_name = user_ar[0]
                user_uid = src["userGroup"]
                #                users[user_name] = user_uid
                users[user_name] = {'uid': user_uid, 'user_type': 'group'}
    else:  # section
        collect_users_from_rulebase(rule["rulebase"])


# collect_users writes user info into global users dict
def collect_users_from_rulebase(rulebase):
    result = ''
    if 'layerchunks' in rulebase:
        for chunk in rulebase['layerchunks']:
            for rule in chunk['rulebase']:
                collect_users_from_rule(rule)
    else:
        for rule in rulebase:
            collect_users_from_rule(rule)
