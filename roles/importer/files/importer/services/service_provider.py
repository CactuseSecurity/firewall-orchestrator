from typing import Callable, Any
from services.global_state import GlobalState
from services.enums import Services, Lifetime



class ServiceProviderEntry:
    def __init__(self, constructor: Callable[[], Any],  lifetime: Lifetime):
        self.constructor = constructor
        self.lifetime = lifetime

class ServiceProvider:
    """
        This class serves as an IOC-container (IOC = inversion of controls) and its purpose is to manage instantiation and lifetime of service classes.
    """

    _instance = None
    _services: dict[Services, ServiceProviderEntry] 
    _singletons: dict[Services, Any]
    _import: dict[tuple[int, Services], Any]
    _management: dict[tuple[int, Services], Any]

    globalStates : dict[tuple[Lifetime, int], GlobalState] = {}


    def __new__(cls):
        if cls._instance is None:
            cls._instance = super(ServiceProvider, cls).__new__(cls)
            cls._instance._services = {}
            cls._instance._singletons = {}
            cls._instance._import = {}

        return cls._instance


    def register(self, key: Services, constructor: Callable[[], Any], lifetime: Lifetime):
        self._services[key] = ServiceProviderEntry(constructor, lifetime)

    

    def get_service(self, key: Services, import_id: int = 0, management_id: int = 0) -> Any:
        """
        Get an instance of a service based on its lifetime. The service will be instantiated if it does not already exist.

        :param key: The service to get.
        :param import_id: The import ID for IMPORT lifetime services.
        :param management_id: The management ID for MANAGEMENT lifetime services.
        :return: The instance of the requested service.
        """
        entry = self._services.get(key)
        service_instance = None

        if not entry:
            raise ValueError(f"Service '{key}' is not registered.")
        
        match entry.lifetime:

            case Lifetime.SINGLETON:
                if key not in self._singletons:
                    self._singletons[key] = entry.constructor()
                service_instance = self._singletons[key]

            case Lifetime.IMPORT:
                import_specific_key = (import_id, key)
                if import_specific_key not in self._import:
                    self._import[import_specific_key] = (entry.constructor())
                service_instance = self._import[import_specific_key]

            case Lifetime.MANAGEMENT:
                management_specific_key = (management_id, key)
                if management_specific_key not in self._management:
                    self._management[management_specific_key] = (entry.constructor())
                service_instance = self._management[management_specific_key]

            case _:
                raise ValueError(f"Unsupported lifetime '{entry.lifetime}' for service '{key}'.")

        return service_instance
    
    
    def dispose_service(self, key: Services, import_id: int = 0, management_id: int = 0):
        """
        Dispose of a service instance based on its lifetime.

        :param key: The service to dispose of.
        :param import_id: The import ID for IMPORT lifetime services.
        :param management_id: The management ID for MANAGEMENT lifetime services.
        """
        entry = self._services.get(key)
        if not entry:
            raise ValueError(f"Service '{key}' is not registered.")

        match entry.lifetime: 

            case Lifetime.SINGLETON:
                if key in self._singletons:
                    del self._singletons[key]

            case Lifetime.IMPORT:
                import_specific_key = (import_id, key)
                if import_specific_key in self._import:
                    del self._import[import_specific_key]

            case Lifetime.MANAGEMENT:
                management_specific_key = (management_id, key)
                if management_specific_key in self._management:
                    del self._management[management_specific_key]

            case _:
                raise ValueError(f"Unsupported lifetime '{entry.lifetime}' for service '{key}'.")

            

    def dispose_scope_import(self, import_id: int):
        """
        Dispose of all services associated with a specific import ID.
        :param import_id: The import ID whose services should be disposed of.
        """
        keys_to_remove = [key for key in self._import if key[0] == import_id]
        for key in keys_to_remove:
            del self._import[key]

    def dispose_scope_management(self, management_id: int):
        """
        Dispose of all services associated with a specific management ID.
        :param management_id: The management ID whose services should be disposed of.
        """
        keys_to_remove = [key for key in self._management if key[0] == management_id]
        for key in keys_to_remove:
            del self._management[key]