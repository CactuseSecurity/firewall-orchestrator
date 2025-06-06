from typing import Callable, Any
from model_controllers.import_state_controller import ImportStateController
from models.fwconfig_normalized import FwConfigNormalized
from services.enums import Services, Lifetime


class ServiceProvider:

    _instance = None

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


    def get_service(self, key: Services, import_state: ImportStateController = None, normalized_config: FwConfigNormalized = None) -> Any:
        entry = self._services.get(key)
        service_instance = None

        if not entry:
            raise ValueError(f"Service '{key}' is not registered.")
        
        if entry["lifetime"] == Lifetime.SINGLETON:
            if key not in self._singletons:
                self._singletons[key] = entry["constructor"]()
            service_instance = self._singletons[key]
        else:
            service_instance = entry["constructor"]()

        if import_state is not None:
            service_instance.import_state = import_state

        if normalized_config is not None:
            service_instance.normalized_config = normalized_config
        
        return service_instance
        