
from fwo_log import getFwoLogger
import json
# from checkpointR8x.cp_getter import ParseUidToName

def collect_users_from_rule(rule, users): #, objDict):
    if 'rule-number' in rule:  # standard rule
        logger = getFwoLogger()
        if 'type' in rule and rule['type'] != 'place-holder':
            for src in rule["source"]:
#                srcObj = ParseUidToName(src, objDict)

                # need to get all details for the user first!
                if 'type' in src:
                    if src['type'] == 'access-role' or src['type'] == 'LegacyUserAtLocation':
                        if src['type'] == 'access-role':
                            user_name = src['name']
                            user_uid = src['uid']
                            user_typ = 'group'
                            user_comment = src.get('comments', None)
                            user_color = src['color']
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
                        users.update({user_name: {'user_uid': user_uid, 'user_typ': user_typ,
                                     'user_comment': user_comment, 'user_color': user_color}})
                else:
                    logger.warning("found src user without type field: " + json.dumps(src))
                    if 'name' in src and 'uid' in src:
                        users.update({src["name"]: {'user_uid': src["uid"], 'user_typ': 'simple'}})

    else:  # section
        collect_users_from_rulebase(rule["rulebase"], users)


# collect_users writes user info into global users dict
def collect_users_from_rulebase(rulebase, users):
    if 'layerchunks' in rulebase:
        for chunk in rulebase['layerchunks']:
            if 'rulebase' in chunk:
                for rule in chunk['rulebase']:
                    collect_users_from_rule(rule, users)
    else:
        for rule in rulebase:
            collect_users_from_rule(rule, users)


# the following is only used within new python-only importer:
def parse_user_objects_from_rulebase(rulebase, users, import_id):
    collect_users_from_rulebase(rulebase, users)
    for user_name in users.keys():
        # TODO: get user info via API
        userUid = getUserUidFromCpApi(user_name)
        # finally add the import id
        users[user_name]['control_id'] = import_id



def getUserUidFromCpApi (userName):
    # show-object with UID
    # dummy implementation returning the name as uid
    return userName

