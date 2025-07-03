from typing import Tuple
import unittest
import sys
import os

sys.path.append(os.path.join(os.path.dirname(__file__), '../importer'))

from importer import fwo_const
from services.group_flats_mapper import GroupFlatsMapper  # type: ignore
from services.uid2id_mapper import Uid2IdMapper  # type: ignore
from services.global_state import GlobalState  # type: ignore
from services.service_provider import ServiceProvider  # type: ignore
from services.enums import Lifetime, Services  # type: ignore
from importer.model_controllers.fwconfig_import import FwConfigImport
from test.mocking.mock_import_state import MockImportStateController
from test.tools.set_up_test import set_up_config_for_import_consistency_test
from test.mocking.mock_config import MockFwConfigNormalizedBuilder


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


def reset_importer_with_new_config(config, mock_api, import_id=0) -> Tuple[FwConfigImport, MockImportStateController]:  # noqa: F821
    service_provider = ServiceProvider()

    import_state = MockImportStateController(import_id)
    import_state.api_connection = mock_api
    global_state = GlobalState()
    global_state.import_state = import_state
    global_state.normalized_config = config

    service_provider.dispose_service(Services.GLOBAL_STATE)
    service_provider.dispose_service(Services.GROUP_FLATS_MAPPER)
    service_provider.dispose_service(Services.UID2ID_MAPPER)
    service_provider.register(Services.GLOBAL_STATE, lambda: global_state,
                              Lifetime.SINGLETON)
    service_provider.register(Services.GROUP_FLATS_MAPPER,
                              lambda: GroupFlatsMapper(), Lifetime.TRANSIENT)
    service_provider.register(Services.UID2ID_MAPPER, lambda: Uid2IdMapper(),
                              Lifetime.SINGLETON)
    config_importer = FwConfigImport()

    return config_importer, import_state

def get_nwobj_member_mapping(config):
    return {
        obj.obj_uid:
        set(obj.obj_member_refs.split(fwo_const.list_delimiter))
        for obj in config.network_objects.values() if obj.obj_typ == "group" and obj.obj_member_refs
    }
def get_svc_member_mapping(config):
    return {
        svc.svc_uid:
        set(svc.svc_member_refs.split(fwo_const.list_delimiter))
        for svc in config.service_objects.values() if svc.svc_typ == "group" and svc.svc_member_refs
    }
def get_nwobj_flat_member_mapping(config, group_flats_mapper):
    return {
        obj.obj_uid:
        set(group_flats_mapper.get_network_object_flats([obj.obj_uid]))
        for obj in config.network_objects.values() if obj.obj_typ == "group"
    }
def get_svc_flat_member_mapping(config, group_flats_mapper):
    return {
        svc.svc_uid:
        set(group_flats_mapper.get_service_object_flats([svc.svc_uid]))
        for svc in config.service_objects.values() if svc.svc_typ == "group"
    }
def get_rule_from_mapping(config):
    return {
        rule.rule_uid:
        set(rule.rule_src_refs.split(fwo_const.list_delimiter))
        for rulebase in config.rulebases
        for rule in rulebase.Rules.values()
    }
def get_rule_svc_mapping(config):
    return {
        rule.rule_uid:
        set(rule.rule_svc_refs.split(fwo_const.list_delimiter))
        for rulebase in config.rulebases
        for rule in rulebase.Rules.values()
    }
def get_rule_nwobj_resolved_mapping(config, group_flats_mapper):
    return {
        rule.rule_uid:
        set(
            group_flats_mapper.get_network_object_flats(
                [ref.split(fwo_const.user_delimiter)[0] for ref in
                    rule.rule_src_refs.split(fwo_const.list_delimiter)]))
        for rulebase in config.rulebases
        for rule in rulebase.Rules.values()
    }
def get_rule_svc_resolved_mapping(config, group_flats_mapper):
    return {
        rule.rule_uid:
        set(
            group_flats_mapper.get_service_object_flats(
                rule.rule_svc_refs.split(fwo_const.list_delimiter)))
        for rulebase in config.rulebases
        for rule in rulebase.Rules.values()
    }

class TestFwoConfigImportConsistency(unittest.TestCase):

    def test_fwconfig_compare_config_against_db_state(self):

        # Arrange
        service_provider = ServiceProvider()

        import_state = MockImportStateController()
        config = set_up_config_for_import_consistency_test()

        global_state = GlobalState()
        global_state.import_state = import_state
        global_state.normalized_config = config

        service_provider.register(Services.GLOBAL_STATE, lambda: global_state,
                                  Lifetime.SINGLETON)
        service_provider.register(Services.GROUP_FLATS_MAPPER,
                                  lambda: GroupFlatsMapper(),
                                  Lifetime.TRANSIENT)
        service_provider.register(Services.UID2ID_MAPPER,
                                  lambda: Uid2IdMapper(), Lifetime.SINGLETON)

        config_importer = FwConfigImport()

        # Act
        config_importer.importConfig()
        mock_api = import_state.api_connection
        config_from_api = mock_api.build_config_from_db(
            import_state, config.rulebases[0].mgm_uid, config.gateways)

        service_provider.dispose_service(Services.GLOBAL_STATE)
        service_provider.dispose_service(Services.GROUP_FLATS_MAPPER)
        service_provider.dispose_service(Services.UID2ID_MAPPER)

        # check if config objects are equal, if a field is not equal, it will raise an AssertionError
        self.assertEqual(
            config, config_from_api,
            f"Config objects are not equal: {find_first_diff(config.dict(), config_from_api.dict())}"
        )

    def test_fwconfig_check_db_member_tables(self):

        # Arrange
        service_provider = ServiceProvider()

        import_state = MockImportStateController()
        config = set_up_config_for_import_consistency_test()

        global_state = GlobalState()
        global_state.import_state = import_state
        global_state.normalized_config = config

        service_provider.register(Services.GLOBAL_STATE, lambda: global_state,
                                  Lifetime.SINGLETON)
        service_provider.register(Services.GROUP_FLATS_MAPPER,
                                  lambda: GroupFlatsMapper(),
                                  Lifetime.TRANSIENT)
        service_provider.register(Services.UID2ID_MAPPER,
                                  lambda: Uid2IdMapper(), Lifetime.SINGLETON)

        config_importer = FwConfigImport()

        # Act
        config_importer.importConfig()
        mock_api = import_state.api_connection

        group_flats_mapper = service_provider.get_service(
            Services.GROUP_FLATS_MAPPER)

        service_provider.dispose_service(Services.GLOBAL_STATE)
        service_provider.dispose_service(Services.GROUP_FLATS_MAPPER)
        service_provider.dispose_service(Services.UID2ID_MAPPER)

        try:
            member_uids_config = get_nwobj_member_mapping(config)
            member_uids_db = mock_api.get_nwobj_member_mappings()

            flat_member_uids_config = get_nwobj_flat_member_mapping(config, group_flats_mapper)
            flat_member_uids_db = mock_api.get_nwobj_flat_member_mappings()

            rule_froms_config = get_rule_from_mapping(config)
            rule_froms_db = mock_api.get_rule_from_mappings()

            rule_nwobj_resolveds_config = get_rule_nwobj_resolved_mapping(config, group_flats_mapper)
            rule_nwobj_resolveds_db = mock_api.get_rule_nwobj_resolved_mappings()
        except Exception as e:
            self.fail(
                f"Failed to retrieve member mappings from the database: {e}")

        self.assertEqual(
            member_uids_config, member_uids_db,
            f"Member UIDs in config and DB do not match: {find_first_diff(member_uids_config, member_uids_db)}"
        )

        self.assertEqual(
            flat_member_uids_config, flat_member_uids_db,
            f"Flat member UIDs in config and DB do not match: {find_first_diff(flat_member_uids_config, flat_member_uids_db)}"
        )

        self.assertEqual(
            rule_froms_config, rule_froms_db,
            f"Rule froms in config and DB do not match: {find_first_diff(rule_froms_config, rule_froms_db)}"
        )

        # self.assertEqual(rule_nwobj_resolveds_config, rule_nwobj_resolveds_db,
        #                     f"Rule resolveds in config and DB do not match: {find_first_diff(rule_nwobj_resolveds_config, rule_nwobj_resolveds_db)}")

    def test_fwconfig_check_db_member_tables_after_deletes(self):
        # Arrange
        service_provider = ServiceProvider()

        import_state = MockImportStateController()
        config = set_up_config_for_import_consistency_test()

        global_state = GlobalState()
        global_state.import_state = import_state
        global_state.normalized_config = config

        service_provider.register(Services.GLOBAL_STATE, lambda: global_state,
                                  Lifetime.SINGLETON)
        service_provider.register(Services.GROUP_FLATS_MAPPER,
                                  lambda: GroupFlatsMapper(),
                                  Lifetime.TRANSIENT)
        service_provider.register(Services.UID2ID_MAPPER,
                                  lambda: Uid2IdMapper(), Lifetime.SINGLETON)

        config_importer = FwConfigImport()

        # Act
        config_importer.importConfig()
        mock_api = import_state.api_connection

        config_builder = MockFwConfigNormalizedBuilder()

        config_builder.change_rule_with_nested_groups(config,
                                                      change_type="delete",
                                                      change_obj="from")
        config_importer, import_state = reset_importer_with_new_config(config, mock_api, import_id=0)
        config_importer.importConfig()

        config_builder.change_rule_with_nested_groups(config,
                                                      change_type="delete",
                                                      change_obj="svc")
        config_importer, import_state = reset_importer_with_new_config(config, mock_api, import_id=1)
        config_importer.importConfig()

        config_builder.change_rule_with_nested_groups(config,
                                                      change_type="delete",
                                                      change_obj="member")
        config_importer, import_state = reset_importer_with_new_config(config, mock_api, import_id=2)
        config_importer.importConfig()

        config_builder.change_rule_with_nested_groups(config,
                                                      change_type="delete",
                                                      change_obj="member_svc")
        config_importer, import_state = reset_importer_with_new_config(config, mock_api, import_id=3)
        config_importer.importConfig()

        config_builder.change_rule_with_nested_groups(
            config, change_type="delete", change_obj="nested_member")
        config_importer, import_state = reset_importer_with_new_config(config, mock_api, import_id=4)
        config_importer.importConfig()

        config_builder.change_rule_with_nested_groups(
            config, change_type="delete", change_obj="nested_member_svc")
        config_importer, import_state = reset_importer_with_new_config(config, mock_api, import_id=5)
        config_importer.importConfig()

        group_flats_mapper = service_provider.get_service(
            Services.GROUP_FLATS_MAPPER)

        service_provider.dispose_service(Services.GLOBAL_STATE)
        service_provider.dispose_service(Services.GROUP_FLATS_MAPPER)
        service_provider.dispose_service(Services.UID2ID_MAPPER)

        member_uids_config = get_nwobj_member_mapping(config)
        member_uids_db = mock_api.get_nwobj_member_mappings()

        svc_member_uids_config = get_svc_member_mapping(config)
        svc_member_uids_db = mock_api.get_svc_member_mappings()

        flat_member_uids_config = get_nwobj_flat_member_mapping(config, group_flats_mapper)
        flat_member_uids_db = mock_api.get_nwobj_flat_member_mappings()

        svc_flat_member_uids_config = get_svc_flat_member_mapping(config, group_flats_mapper)
        svc_flat_member_uids_db = mock_api.get_svc_flat_member_mappings()

        rule_froms_config = get_rule_from_mapping(config)
        rule_froms_db = mock_api.get_rule_from_mappings()

        rule_svcs_config = get_rule_svc_mapping(config)
        rule_svcs_db = mock_api.get_rule_svc_mappings()

        rule_nwobj_resolveds_config = get_rule_nwobj_resolved_mapping(config, group_flats_mapper)
        rule_nwobj_resolveds_db = mock_api.get_rule_nwobj_resolved_mappings()

        rule_svc_resolveds_config = get_rule_svc_resolved_mapping(config, group_flats_mapper)
        rule_svc_resolveds_db = mock_api.get_rule_svc_resolved_mappings()

        self.assertEqual(
            member_uids_config, member_uids_db,
            f"Member UIDs in config and DB do not match: {find_first_diff(member_uids_config, member_uids_db)}"
        )
        self.assertEqual(
            svc_member_uids_config, svc_member_uids_db,
            f"Service member UIDs in config and DB do not match: {find_first_diff(svc_member_uids_config, svc_member_uids_db)}"
        )
        self.assertEqual(
            flat_member_uids_config, flat_member_uids_db,
            f"Flat member UIDs in config and DB do not match: {find_first_diff(flat_member_uids_config, flat_member_uids_db)}"
        )
        self.assertEqual(
            svc_flat_member_uids_config, svc_flat_member_uids_db,
            f"Service flat member UIDs in config and DB do not match: {find_first_diff(svc_flat_member_uids_config, svc_flat_member_uids_db)}"
        )
        self.assertEqual(
            rule_froms_config, rule_froms_db,
            f"Rule froms in config and DB do not match: {find_first_diff(rule_froms_config, rule_froms_db)}"
        )
        self.assertEqual(
            rule_svcs_config, rule_svcs_db,
            f"Rule services in config and DB do not match: {find_first_diff(rule_svcs_config, rule_svcs_db)}"
        )
        # self.assertEqual(rule_nwobj_resolveds_config, rule_nwobj_resolveds_db,
        #                     f"Rule resolveds in config and DB do not match: {find_first_diff(rule_nwobj_resolveds_config, rule_nwobj_resolveds_db)}")
        # self.assertEqual(rule_svc_resolveds_config, rule_svc_resolveds_db,
        #                     f"Rule service resolveds in config and DB do not match: {find_first_diff(rule_svc_resolveds_config, rule_svc_resolveds_db)}")
        
        config_from_api = mock_api.build_config_from_db(
            import_state, config.rulebases[0].mgm_uid, config.gateways)
        
        self.assertEqual(
            config, config_from_api,
            f"Config objects are not equal after import with deletions: {find_first_diff(config.dict(), config_from_api.dict())}"
        )

    def test_fwconfig_check_db_member_tables_after_adds(self):
        # Arrange
        service_provider = ServiceProvider()

        import_state = MockImportStateController()
        config = set_up_config_for_import_consistency_test()

        global_state = GlobalState()
        global_state.import_state = import_state
        global_state.normalized_config = config

        service_provider.register(Services.GLOBAL_STATE, lambda: global_state,
                                  Lifetime.SINGLETON)
        service_provider.register(Services.GROUP_FLATS_MAPPER,
                                  lambda: GroupFlatsMapper(),
                                  Lifetime.TRANSIENT)
        service_provider.register(Services.UID2ID_MAPPER,
                                  lambda: Uid2IdMapper(), Lifetime.SINGLETON)

        config_importer = FwConfigImport()

        # Act
        config_importer.importConfig()
        config_importer.storeLatestConfig()
        mock_api = import_state.api_connection

        config_builder = MockFwConfigNormalizedBuilder()

        config_builder.change_rule_with_nested_groups(config,
                                                      change_type="add",
                                                      change_obj="from")
        config_importer, import_state = reset_importer_with_new_config(config, mock_api, import_id=1)
        config_importer.importConfig()
        config_importer.storeLatestConfig()

        config_builder.change_rule_with_nested_groups(config,
                                                      change_type="add",
                                                      change_obj="svc")
        config_importer, import_state = reset_importer_with_new_config(config, mock_api, import_id=2)
        config_importer.importConfig()
        config_importer.storeLatestConfig()

        config_builder.change_rule_with_nested_groups(config,
                                                      change_type="add",
                                                      change_obj="member")
        config_importer, import_state = reset_importer_with_new_config(config, mock_api, import_id=3)
        config_importer.importConfig()
        config_importer.storeLatestConfig()

        config_builder.change_rule_with_nested_groups(config,
                                                      change_type="add",
                                                      change_obj="member_svc")
        config_importer, import_state = reset_importer_with_new_config(config, mock_api, import_id=4)
        config_importer.importConfig()
        config_importer.storeLatestConfig()

        config_builder.change_rule_with_nested_groups(
            config, change_type="add", change_obj="nested_member")
        config_importer, import_state = reset_importer_with_new_config(config, mock_api, import_id=5)
        config_importer.importConfig()
        config_importer.storeLatestConfig()

        config_builder.change_rule_with_nested_groups(
            config, change_type="add", change_obj="nested_member_svc")
        config_importer, import_state = reset_importer_with_new_config(config, mock_api, import_id=6)
        config_importer.importConfig()
        config_importer.storeLatestConfig()

        group_flats_mapper = service_provider.get_service(
            Services.GROUP_FLATS_MAPPER)

        service_provider.dispose_service(Services.GLOBAL_STATE)
        service_provider.dispose_service(Services.GROUP_FLATS_MAPPER)
        service_provider.dispose_service(Services.UID2ID_MAPPER)

        member_uids_config = {
            obj.obj_uid:
            set(obj.obj_member_refs.split(fwo_const.list_delimiter))
            for obj in config.network_objects.values() if obj.obj_typ == "group" and obj.obj_member_refs
        }
        member_uids_db = mock_api.get_nwobj_member_mappings()
        svc_member_uids_config = {
            svc.svc_uid:
            set(svc.svc_member_refs.split(fwo_const.list_delimiter))
            for svc in config.service_objects.values() if svc.svc_typ == "group" and svc.svc_member_refs
        }
        svc_member_uids_db = mock_api.get_svc_member_mappings()
        flat_member_uids_config = {
            obj.obj_uid:
            set(group_flats_mapper.get_network_object_flats([obj.obj_uid]))
            for obj in config.network_objects.values() if obj.obj_typ == "group"
        }
        flat_member_uids_db = mock_api.get_nwobj_flat_member_mappings()
        svc_flat_member_uids_config = {
            svc.svc_uid:
            set(group_flats_mapper.get_service_object_flats([svc.svc_uid]))
            for svc in config.service_objects.values() if svc.svc_typ == "group"
        }
        svc_flat_member_uids_db = mock_api.get_svc_flat_member_mappings()
        rule_froms_config = {
            rule.rule_uid:
            set(rule.rule_src_refs.split(fwo_const.list_delimiter))
            for rulebase in config.rulebases
            for rule in rulebase.Rules.values()
        }
        rule_froms_db = mock_api.get_rule_from_mappings()
        rule_svcs_config = {
            rule.rule_uid:
            set(rule.rule_svc_refs.split(fwo_const.list_delimiter))
            for rulebase in config.rulebases
            for rule in rulebase.Rules.values()
        }
        rule_svcs_db = mock_api.get_rule_svc_mappings()
        rule_nwobj_resolveds_config = {
            rule.rule_uid:
            set(
                group_flats_mapper.get_network_object_flats(
                    [ref.split(fwo_const.user_delimiter)[0] for ref in
                     rule.rule_src_refs.split(fwo_const.list_delimiter)]))
            for rulebase in config.rulebases
            for rule in rulebase.Rules.values()
        }
        rule_nwobj_resolveds_db = mock_api.get_rule_nwobj_resolved_mappings()
        rule_svc_resolveds_config = {
            rule.rule_uid:
            set(
                group_flats_mapper.get_service_object_flats(
                    rule.rule_svc_refs.split(fwo_const.list_delimiter)))
            for rulebase in config.rulebases
            for rule in rulebase.Rules.values()
        }
        rule_svc_resolveds_db = mock_api.get_rule_svc_resolved_mappings()
        self.assertEqual(
            member_uids_config, member_uids_db,
            f"Member UIDs in config and DB do not match: {find_first_diff(member_uids_config, member_uids_db)}"
        )
        self.assertEqual(
            svc_member_uids_config, svc_member_uids_db,
            f"Service member UIDs in config and DB do not match: {find_first_diff(svc_member_uids_config, svc_member_uids_db)}"
        )
        self.assertEqual(
            flat_member_uids_config, flat_member_uids_db,
            f"Flat member UIDs in config and DB do not match: {find_first_diff(flat_member_uids_config, flat_member_uids_db)}"
        )
        self.assertEqual(
            svc_flat_member_uids_config, svc_flat_member_uids_db,
            f"Service flat member UIDs in config and DB do not match: {find_first_diff(svc_flat_member_uids_config, svc_flat_member_uids_db)}"
        )
        self.assertEqual(
            rule_froms_config, rule_froms_db,
            f"Rule froms in config and DB do not match: {find_first_diff(rule_froms_config, rule_froms_db)}"
        )
        self.assertEqual(
            rule_svcs_config, rule_svcs_db,
            f"Rule services in config and DB do not match: {find_first_diff(rule_svcs_config, rule_svcs_db)}"
        )
        # self.assertEqual(rule_nwobj_resolveds_config, rule_nwobj_resolveds_db,
        #                     f"Rule resolveds in config and DB do not match: {find_first_diff(rule_nwobj_resolveds_config, rule_nwobj_resolveds_db)}")
        # self.assertEqual(rule_svc_resolveds_config, rule_svc_resolveds_db,
        #                     f"Rule service resolveds in config and DB do not match: {find_first_diff(rule_svc_resolveds_config, rule_svc_resolveds_db)}")

        config_from_api = mock_api.build_config_from_db(
            import_state, config.rulebases[0].mgm_uid, config.gateways)
        self.assertEqual(
            config, config_from_api,
            f"Config objects are not equal after import with additions: {find_first_diff(config.dict(), config_from_api.dict())}"
        )
    
    def test_fwconfig_check_db_member_tables_after_changes(self):
        # Arrange
        service_provider = ServiceProvider()

        import_state = MockImportStateController()
        config = set_up_config_for_import_consistency_test()

        global_state = GlobalState()
        global_state.import_state = import_state
        global_state.normalized_config = config

        service_provider.register(Services.GLOBAL_STATE, lambda: global_state,
                                  Lifetime.SINGLETON)
        service_provider.register(Services.GROUP_FLATS_MAPPER,
                                  lambda: GroupFlatsMapper(),
                                  Lifetime.TRANSIENT)
        service_provider.register(Services.UID2ID_MAPPER,
                                  lambda: Uid2IdMapper(), Lifetime.SINGLETON)

        config_importer = FwConfigImport()

        # Act
        config_importer.importConfig()
        mock_api = import_state.api_connection

        config_builder = MockFwConfigNormalizedBuilder()

        config_builder.change_rule_with_nested_groups(config,
                                                      change_type="change",
                                                      change_obj="from")
        config_importer, import_state = reset_importer_with_new_config(config, mock_api, import_id=1)
        config_importer.importConfig()
        config_importer.storeLatestConfig()
        config_builder.change_rule_with_nested_groups(config,
                                                      change_type="change",
                                                      change_obj="svc")
        config_importer, import_state = reset_importer_with_new_config(config, mock_api, import_id=2)
        config_importer.importConfig()
        config_importer.storeLatestConfig()
        config_builder.change_rule_with_nested_groups(config,
                                                      change_type="change",
                                                      change_obj="member")
        config_importer, import_state = reset_importer_with_new_config(config, mock_api, import_id=3)
        config_importer.importConfig()
        config_importer.storeLatestConfig()
        config_builder.change_rule_with_nested_groups(config,
                                                      change_type="change",
                                                      change_obj="member_svc")
        config_importer, import_state = reset_importer_with_new_config(config, mock_api, import_id=4)
        config_importer.importConfig()
        config_importer.storeLatestConfig()
        config_builder.change_rule_with_nested_groups(
            config, change_type="change", change_obj="nested_member")
        config_importer, import_state = reset_importer_with_new_config(config, mock_api, import_id=5)
        config_importer.importConfig()
        config_importer.storeLatestConfig()
        config_builder.change_rule_with_nested_groups(
            config, change_type="change", change_obj="nested_member_svc")
        config_importer, import_state = reset_importer_with_new_config(config, mock_api, import_id=6)
        config_importer.importConfig()
        config_importer.storeLatestConfig()
        group_flats_mapper = service_provider.get_service(
            Services.GROUP_FLATS_MAPPER)
        service_provider.dispose_service(Services.GLOBAL_STATE)
        service_provider.dispose_service(Services.GROUP_FLATS_MAPPER)
        service_provider.dispose_service(Services.UID2ID_MAPPER)
        member_uids_config = {
            obj.obj_uid:
            set(obj.obj_member_refs.split(fwo_const.list_delimiter))
            for obj in config.network_objects.values() if obj.obj_typ == "group" and obj.obj_member_refs
        }
        member_uids_db = mock_api.get_nwobj_member_mappings()
        svc_member_uids_config = {
            svc.svc_uid:
            set(svc.svc_member_refs.split(fwo_const.list_delimiter))
            for svc in config.service_objects.values() if svc.svc_typ == "group" and svc.svc_member_refs
        }
        svc_member_uids_db = mock_api.get_svc_member_mappings()
        flat_member_uids_config = {
            obj.obj_uid:
            set(group_flats_mapper.get_network_object_flats([obj.obj_uid]))
            for obj in config.network_objects.values() if obj.obj_typ == "group"
        }
        flat_member_uids_db = mock_api.get_nwobj_flat_member_mappings()
        svc_flat_member_uids_config = {
            svc.svc_uid:
            set(group_flats_mapper.get_service_object_flats([svc.svc_uid]))
            for svc in config.service_objects.values() if svc.svc_typ == "group"
        }
        svc_flat_member_uids_db = mock_api.get_svc_flat_member_mappings()
        rule_froms_config = {
            rule.rule_uid:
            set(rule.rule_src_refs.split(fwo_const.list_delimiter))
            for rulebase in config.rulebases
            for rule in rulebase.Rules.values()
        }
        rule_froms_db = mock_api.get_rule_from_mappings()
        rule_svcs_config = {
            rule.rule_uid:
            set(rule.rule_svc_refs.split(fwo_const.list_delimiter))
            for rulebase in config.rulebases
            for rule in rulebase.Rules.values()
        }
        rule_svcs_db = mock_api.get_rule_svc_mappings()
        rule_nwobj_resolveds_config = {
            rule.rule_uid:
            set(
                group_flats_mapper.get_network_object_flats(
                    [ref.split(fwo_const.user_delimiter)[0] for ref in
                     rule.rule_src_refs.split(fwo_const.list_delimiter)]))
            for rulebase in config.rulebases
            for rule in rulebase.Rules.values()
        }
        rule_nwobj_resolveds_db = mock_api.get_rule_nwobj_resolved_mappings()
        rule_svc_resolveds_config = {
            rule.rule_uid:
            set(
                group_flats_mapper.get_service_object_flats(
                    rule.rule_svc_refs.split(fwo_const.list_delimiter)))
            for rulebase in config.rulebases
            for rule in rulebase.Rules.values()
        }
        rule_svc_resolveds_db = mock_api.get_rule_svc_resolved_mappings()
        self.assertEqual(
            member_uids_config, member_uids_db,
            f"Member UIDs in config and DB do not match: {find_first_diff(member_uids_config, member_uids_db)}"
        )
        self.assertEqual(
            svc_member_uids_config, svc_member_uids_db,
            f"Service member UIDs in config and DB do not match: {find_first_diff(svc_member_uids_config, svc_member_uids_db)}"
        )
        self.assertEqual(
            flat_member_uids_config, flat_member_uids_db,
            f"Flat member UIDs in config and DB do not match: {find_first_diff(flat_member_uids_config, flat_member_uids_db)}"
        )
        self.assertEqual(
            svc_flat_member_uids_config, svc_flat_member_uids_db,
            f"Service flat member UIDs in config and DB do not match: {find_first_diff(svc_flat_member_uids_config, svc_flat_member_uids_db)}"
        )
        self.assertEqual(
            rule_froms_config, rule_froms_db,
            f"Rule froms in config and DB do not match: {find_first_diff(rule_froms_config, rule_froms_db)}"
        )
        self.assertEqual(
            rule_svcs_config, rule_svcs_db,
            f"Rule services in config and DB do not match: {find_first_diff(rule_svcs_config, rule_svcs_db)}"
        )
        # self.assertEqual(rule_nwobj_resolveds_config, rule_nwobj_resolveds_db,
        #                     f"Rule resolveds in config and DB do not match: {find_first_diff(rule_nwobj_resolveds_config, rule_nwobj_resolveds_db)}")
        # self.assertEqual(rule_svc_resolveds_config, rule_svc_resolveds_db,
        #                     f"Rule service resolveds in config and DB do not match: {find_first_diff(rule_svc_resolveds_config, rule_svc_resolveds_db)}")
        config_from_api = mock_api.build_config_from_db(
            import_state, config.rulebases[0].mgm_uid, config.gateways)
        self.assertEqual(
            config, config_from_api,
            f"Config objects are not equal after import with changes: {find_first_diff(config.dict(), config_from_api.dict())}"
        )