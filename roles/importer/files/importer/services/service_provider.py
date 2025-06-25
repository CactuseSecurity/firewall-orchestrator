from typing import Callable, Any
from importer.services.enums import Services, Lifetime


class ServiceProvider:
    """
        This class serves as an IOC-container (IOC = inversion of controls) and its purpose is to manage instantiation and lifetime of service classes.
    """

    _instance = None
    _services: dict
    _singletons: dict
    _import: dict


    def __new__(cls):
        if cls._instance is None:
            cls._instance = super(ServiceProvider, cls).__new__(cls)
            cls._instance._services = {}
            cls._instance._singletons = {}
            cls._instance._import = {}

        return cls._instance


    def register(self, key: Services, constructor: Callable[[], Any], lifetime: Lifetime = Lifetime.TRANSIENT):
        self._services[key] = {
            "constructor": constructor,
            "lifetime": lifetime
        }


    def get_service(self, key: Services, import_id: int = 0) -> Any:
        entry = self._services.get(key)
        service_instance = None

        if not entry:
            raise ValueError(f"Service '{key}' is not registered.")
        
        match entry["lifetime"]:

            case Lifetime.SINGLETON:
                if key not in self._singletons:
                    self._singletons[key] = entry["constructor"]()
                service_instance = self._singletons[key]

            case Lifetime.IMPORT:
                import_specific_key = (import_id, key)
                if import_specific_key not in self._import:
                    self._import[import_specific_key] = (entry["constructor"]())
                service_instance = self._import[import_specific_key]

            case _:
                service_instance = entry["constructor"]()

        return service_instance
    
    
    def dispose_service(self, key: Services, import_id: int = 0):
        entry = self._services.get(key)
        if not entry:
            raise ValueError(f"Service '{key}' is not registered.")

        match entry["lifetime"]:

            case Lifetime.SINGLETON:
                if key in self._singletons:
                    del self._singletons[key]

            case Lifetime.IMPORT:
                import_specific_key = (import_id, key)
                if import_specific_key in self._import:
                    del self._import[import_specific_key]

