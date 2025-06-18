import unittest
import sys
import os

sys.path.append(os.path.join(os.path.dirname(__file__), '../importer'))

from importer.model_controllers.fwconfig_import import FwConfigImport
from test.mocking.mock_import_state import MockImportStateController
from test.tools.set_up_test import set_up_config_for_import_consistency_test
from test.mocking.mock_config import sort_config

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

    def test_import_config_fetch_from_db_check_consistency(self):
        
        # Arrange

        import_state = MockImportStateController()
        config = set_up_config_for_import_consistency_test()
        config_importer = FwConfigImport(import_state, config)
        config_importer.importConfig()
        mock_api = import_state.api_connection
        config_from_api = mock_api.build_config_from_db(import_state, config.rulebases[0].mgm_uid, config.gateways)

        # Act

        # check if config objects are equal, if a field is not equal, it will raise an AssertionError
        self.assertEqual(config, config_from_api, 
                         f"Config objects are not equal: {find_first_diff(config.dict(), config_from_api.dict())}")

