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

from  importer.checkpointR8x.fwcommon import isCompatibleApiVersion


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


if __name__ == '__main__':
    unittest.main()