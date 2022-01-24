import common

def csv_dump_user(user_name, user, import_id):
    user_line =  common.csv_add_field(import_id)                        # control_id
    user_line += common.csv_add_field(user_name)                        # user_name
    user_line += common.csv_add_field(user['user_typ'])                 # user_typ
    if 'user_member_names' in user:   
        user_line += common.csv_add_field(user['user_member_names'])    # user_member_names
    else:  
        user_line += common.csv_delimiter                               # no user_member_names
    if 'user_member_refs' in user:  
        user_line += common.csv_add_field(user['user_member_refs'])     # user_member_refs
    else:   
        user_line += common.csv_delimiter                               # no user_member_refs
    if 'user_color' in user: 
        user_line += common.csv_add_field(user['user_color'])           # user_color
    else:
        user_line += common.csv_delimiter                               # no user_color
    if 'user_comment' in user and user['user_comment']!=None and user['user_comment']!='':
        user_line += common.csv_add_field(user['user_comment'])         # user_comment
    else:
        user_line += common.csv_delimiter                               # no user_comment
    user_line += common.csv_add_field(user['user_uid'])                 # user_uid
    user_line += common.csv_delimiter                                   # user_valid_until
    user_line += common.line_delimiter                                  # last_change_admin
    return user_line
