import unittest
import sys
import os

sys.path.append(os.path.join(os.path.dirname(__file__), '../importer'))

from importer import fwo_const
from importer.services.group_flats_mapper import GroupFlatsMapper
from importer.services.uid2id_mapper import Uid2IdMapper
from importer.services.global_state import GlobalState
from importer.services.service_provider import ServiceProvider
from importer.services.enums import Lifetime, Services
from importer.model_controllers.fwconfig_import import FwConfigImport
from test.mocking.mock_import_state import MockImportStateController
from test.tools.set_up_test import set_up_config_for_import_consistency_test

def find_first_diff(a, b, path="root"):
    if type(a) is not type(b):
        return f"Type mismatch at {path}: {type(a)} != {type(b)}"
    if isinstance(a, dict):
        for k in a:
            if k not in b:
                return f"Key '{k}' missing in second object at {path}"
            res = find_first_diff(a[k], b[k], f"{path}.{k}")
            if res:
                return res
        for k in b:
            if k not in a:
                return f"Key '{k}' missing in first object at {path}"
    elif isinstance(a, list):
        for i, (x, y) in enumerate(zip(a, b)):
            res = find_first_diff(x, y, f"{path}[{i}]")
            if res:
                return res
        if len(a) != len(b):
            return f"List length mismatch at {path}: {len(a)} != {len(b)}"
    else:
        if a != b:
            return f"Value mismatch at {path}: {a} != {b}"
    return None


class TestFwoConfigImportConsistency(unittest.TestCase):

    def test_fwconfig_compare_config_against_db_state(self):
        
        # Arrange
        service_provider = ServiceProvider()

        import_state = MockImportStateController()
        config = set_up_config_for_import_consistency_test()

        global_state = GlobalState()
        global_state.import_state = import_state
        global_state.normalized_config = config

        service_provider.register(Services.GLOBAL_STATE, lambda: global_state, Lifetime.SINGLETON)
        service_provider.register(Services.GROUP_FLATS_MAPPER, lambda: GroupFlatsMapper(), Lifetime.TRANSIENT)
        service_provider.register(Services.UID2ID_MAPPER, lambda: Uid2IdMapper(), Lifetime.SINGLETON)

        config_importer = FwConfigImport()

        # Act
        config_importer.importConfig()
        mock_api = import_state.api_connection
        config_from_api = mock_api.build_config_from_db(import_state, config.rulebases[0].mgm_uid, config.gateways)

        service_provider.dispose_service(Services.GLOBAL_STATE)
        service_provider.dispose_service(Services.GROUP_FLATS_MAPPER)
        service_provider.dispose_service(Services.UID2ID_MAPPER)

        # check if config objects are equal, if a field is not equal, it will raise an AssertionError
        self.assertEqual(config, config_from_api, 
                         f"Config objects are not equal: {find_first_diff(config.dict(), config_from_api.dict())}")


    def test_fwconfig_check_db_member_tables(self):
        
        # Arrange
        service_provider = ServiceProvider()

        import_state = MockImportStateController()
        config = set_up_config_for_import_consistency_test()

        global_state = GlobalState()
        global_state.import_state = import_state
        global_state.normalized_config = config

        service_provider.register(Services.GLOBAL_STATE, lambda: global_state, Lifetime.SINGLETON)
        service_provider.register(Services.GROUP_FLATS_MAPPER, lambda: GroupFlatsMapper(), Lifetime.TRANSIENT)
        service_provider.register(Services.UID2ID_MAPPER, lambda: Uid2IdMapper(), Lifetime.SINGLETON)

        config_importer = FwConfigImport()

        # Act
        config_importer.importConfig()
        mock_api = import_state.api_connection
        uid2id_mapper = service_provider.get_service(Services.UID2ID_MAPPER)

        member_uids_config = {obj.obj_uid: set(obj.obj_member_refs.split(fwo_const.list_delimiter)) 
                              for obj in config.network_objects.values() if obj.obj_member_refs}
        member_uids_db = {}
        for objgrp in mock_api.get_table("objgrp").values():
            objgrp_id = objgrp["objgrp_id"]
            uid = next((uid for uid, id in uid2id_mapper.nwobj_uid2id.items() if id == objgrp_id), None)
            if uid is None:
                self.fail(f"Object group ID {objgrp_id} not found in UID2ID mapper.")
            if uid not in member_uids_db:
                member_uids_db[uid] = set()
            member_id = objgrp["objgrp_member_id"]
            member_uid_db = next((uid for uid, id in uid2id_mapper.nwobj_uid2id.items() if id == member_id), None)
            if member_uid_db is None:
                self.fail(f"Member ID {member_id} not found in UID2ID mapper.")
            member_uids_db[uid].add(member_uid_db)
        
        service_provider.dispose_service(Services.GLOBAL_STATE)
        service_provider.dispose_service(Services.GROUP_FLATS_MAPPER)
        service_provider.dispose_service(Services.UID2ID_MAPPER)

        self.assertEqual(member_uids_config, member_uids_db,
                            f"Member UIDs in config and DB do not match: {find_first_diff(member_uids_config, member_uids_db)}")
        #TODO: check flat groups as well
        

    #TODO: add tests for import with changed config (changed ip in member obj in nested group)