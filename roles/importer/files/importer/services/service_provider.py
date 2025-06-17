from typing import Callable, Any
from services.enums import Services, Lifetime


class ServiceProvider:

    _instance = None
    _services: dict
    _singletons: dict


    def __new__(cls):
        if cls._instance is None:
            cls._instance = super(ServiceProvider, cls).__new__(cls)
            cls._instance._services = {}
            cls._instance._singletons = {}

        return cls._instance


    def register(self, key: Services, constructor: Callable[[], Any], lifetime: Lifetime = Lifetime.TRANSIENT):
        self._services[key] = {
            "constructor": constructor,
            "lifetime": lifetime
        }


    def get_service(self, key: Services) -> Any:
        entry = self._services.get(key)
        service_instance = None

        if not entry:
            raise ValueError(f"Service '{key}' is not registered.")
        
        # Create new instance for transient services and return instance of singleton services.

        if entry["lifetime"] == Lifetime.SINGLETON:
            if key not in self._singletons:
                self._singletons[key] = entry["constructor"]()
            service_instance = self._singletons[key]
        else:
            service_instance = entry["constructor"]()

        return service_instance

