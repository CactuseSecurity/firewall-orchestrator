import importlib.util
import os


def load_module(module_name, file_name):
    """Loads a module dynamically from the importer directory. Only supported from direct child directories."""
    module_path = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "importer", file_name))
    spec = importlib.util.spec_from_file_location(module_name, module_path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module
