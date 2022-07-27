import common

def normalize_users(full_config, config2import, import_id, user_scope):
    users = []
    for scope in user_scope:
        for user_orig in full_config[scope]:
            name = None
            type = 'simple'
            color = None
            member_names = None

            if 'member' in user_orig:
                type = 'group'
                member_names = ''
                for member in user_orig['member']:
                    member_names += member + common.list_delimiter
                member_names = member_names[:-1]
            if 'name' in user_orig:
                name = str(user_orig['name'])
            if 'comment' in user_orig:
                name = str(user_orig['comment'])
            if 'uid' in user_orig:
                name = str(user_orig['uid'])
            if 'color' in user_orig and str(user_orig['color']) != 0:
                color = str(user_orig['color'])
            
            addUser(users, type, name, color, member_names, import_id)

    config2import.update({'user_objects': users})


def addUser(user_objects, type, name, color, member_names, import_id):
    user_objects.extend([{'user_typ': type,
                        'user_name': name, 
                        'user_color': color,
                        'user_uid': name, 
                        'user_comment': None,
                        'user_member_refs': member_names,
                        'user_member_names': member_names,
                        'control_id': import_id
                        }])
