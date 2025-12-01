__version__ = "2025-11-20-01"
# revision history:
# 2025-11-20-01, initial version


def transform_owner_dict_to_list(app_data):
    owner_data = {"owners": []}
    for app_id in app_data:
        owner_data['owners'].append(app_data[app_id].to_json())
    return owner_data


def transform_app_list_to_dict(app_list):
    app_data_dict = {}
    for app in app_list:
        app_data_dict[app.app_id_external] = app
    return app_data_dict
