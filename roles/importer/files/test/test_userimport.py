import sys
import os
import unittest
import importlib.util

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..')))
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..', '..')))
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
os.path.abspath(os.path.join(os.path.dirname(__file__), '..', 'importer'))
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '..', 'importer', 'model_controllers')))

def load_module(module_name, file_name):
    """Lädt ein Modul dynamisch aus dem importer-Ordner."""
    module_path = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', 'importer', file_name))
    spec = importlib.util.spec_from_file_location(module_name, module_path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module

load_module("fwo_const", "fwo_const.py")
load_module("common", "common.py")

from importer.checkpointR8x.fwcommon import isCompatibleApiVersion
from importer.checkpointR8x.cp_user import normalizeUsers, normalizeUsersLegacy, collect_user_objects
from test.mocking.mock_config import ConfigMocker


class TestUserImport(unittest.TestCase):
    def test_isCompatibleApiVersion(self):
        versions_under_test = [
            "1.5",
            "1.6.0.1",
            "1.6.1",
            "1.6.1.0.1",
            "1.6.2",
            "1.7",
            "something-else"
        ]
        required_version_under_test = "1.6.1"
        results = {}
        last_entry_raised_value_error = False

        for version in versions_under_test:
            try:   
                result = isCompatibleApiVersion(required_version_under_test, version)
                results[version] = result
            except ValueError:
                if version == versions_under_test[-1]:
                    last_entry_raised_value_error = True

        self.assertFalse(results["1.5"])
        self.assertFalse(results["1.6.0.1"])
        self.assertTrue(results["1.6.1"])
        self.assertTrue(results["1.6.1.0.1"])
        self.assertTrue(results["1.6.2"])
        self.assertTrue(results["1.7"])
        self.assertTrue(last_entry_raised_value_error)

    def test_collect_user_objects(self):
        # Arrange
        native_config_mocker = ConfigMocker()
        native_config_mocker.config_type = "checkpoint"
        native_config_mocker.checkpoint_objects_config = { 
            "users": 5, 
            "access-roles": 5, 
            "user-templates": 5 
        }
        expected_object_count = sum(native_config_mocker.checkpoint_objects_config.values())
        mocked_objects, config_mock = native_config_mocker.create_config(True)
        user_objects  = []
        

        # Act
        for object_table in config_mock["object_tables"]:
            collect_user_objects(object_table, user_objects)

        # Assert
        self.assertEqual(len(user_objects), expected_object_count)
        for user in user_objects:
            mock_object = next(object for object in mocked_objects if object["uid"] == user["usr_uid"])
            self.assertEqual(user["usr_name"], mock_object["name"])
            if mock_object["type"] == "access-roles" or mock_object["type"] == "user-templates":
                self.assertEqual(user["usr_type"], "undef")
            if mock_object["type"] == "users":
                self.assertEqual(user["usr_type"], "simple")
            if mock_object["type"] == "user-groups":
                self.assertEqual(user["usr_type"], "group")


    def test_normalizeUsers(self):
        # Assert
        native_config_mocker = ConfigMocker()
        native_config_mocker.config_type = "checkpoint"
        native_config_mocker.checkpoint_objects_config = { 
            "users": 5, 
            "access-roles": 5, 
            "user-templates": 5 
        }
        expected_object_count = sum(native_config_mocker.checkpoint_objects_config.values())
        mocked_objects, native_config_mock = native_config_mocker.create_config(True)

        normalized_config_mocker = ConfigMocker()
        normalized_config_mock, _ = normalized_config_mocker.create_config(True, number_config=[])

        import_id = 42

        # Act
        normalizeUsers(native_config_mock, normalized_config_mock.config, import_id)

        # Assert
        self.assertIn('user_objects', normalized_config_mock.config)
        self.assertEqual(len(normalized_config_mock.config['user_objects']), expected_object_count)
        
        for obj in normalized_config_mock.config['user_objects']:
            self.assertIn('control_id', obj)
            self.assertEqual(obj['control_id'], import_id)



    def test_normalizeUsersLegacy(self):
        self.fail("Not implemented yet.")   

if __name__ == '__main__':
    unittest.main()