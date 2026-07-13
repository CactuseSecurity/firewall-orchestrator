import pytest
from services.enums import Lifetime, Services
from services.service_provider import ServiceProvider


class TestServiceProvider:
    def setup_method(self):
        ServiceProvider().reset()

    def test_singleton_service_is_reused_until_disposed(self):
        provider = ServiceProvider()
        created: list[object] = []

        provider.register(Services.GLOBAL_STATE, lambda: created.append(object()) or created[-1], Lifetime.SINGLETON)

        first_instance = provider.get_service(Services.GLOBAL_STATE)
        second_instance = provider.get_service(Services.GLOBAL_STATE)
        provider.dispose_service(Services.GLOBAL_STATE)
        third_instance = provider.get_service(Services.GLOBAL_STATE)

        assert first_instance is second_instance
        assert third_instance is not first_instance

    def test_import_service_is_scoped_by_import_id(self):
        provider = ServiceProvider()
        provider.register(Services.UID2ID_MAPPER, object, Lifetime.IMPORT)

        first_import_instance = provider.get_service(Services.UID2ID_MAPPER, import_id=1)
        same_import_instance = provider.get_service(Services.UID2ID_MAPPER, import_id=1)
        second_import_instance = provider.get_service(Services.UID2ID_MAPPER, import_id=2)
        provider.dispose_service(Services.UID2ID_MAPPER, import_id=1)
        recreated_first_import_instance = provider.get_service(Services.UID2ID_MAPPER, import_id=1)

        assert first_import_instance is same_import_instance
        assert second_import_instance is not first_import_instance
        assert recreated_first_import_instance is not first_import_instance

    def test_management_service_is_scoped_by_management_id(self):
        provider = ServiceProvider()
        provider.register(Services.FWO_CONFIG, object, Lifetime.MANAGEMENT)

        first_management_instance = provider.get_service(Services.FWO_CONFIG, management_id=1)
        same_management_instance = provider.get_service(Services.FWO_CONFIG, management_id=1)
        second_management_instance = provider.get_service(Services.FWO_CONFIG, management_id=2)
        provider.dispose_service(Services.FWO_CONFIG, management_id=1)
        recreated_first_management_instance = provider.get_service(Services.FWO_CONFIG, management_id=1)

        assert first_management_instance is same_management_instance
        assert second_management_instance is not first_management_instance
        assert recreated_first_management_instance is not first_management_instance

    def test_unregistered_service_fails(self):
        provider = ServiceProvider()

        with pytest.raises(ValueError, match="not registered"):
            provider.get_service(Services.GLOBAL_STATE)

        with pytest.raises(ValueError, match="not registered"):
            provider.dispose_service(Services.GLOBAL_STATE)

    def test_unsupported_lifetime_fails(self):
        provider = ServiceProvider()
        provider.register(Services.GLOBAL_STATE, object, Lifetime.TRANSIENT)

        with pytest.raises(ValueError, match="Unsupported lifetime"):
            provider.get_service(Services.GLOBAL_STATE)

        with pytest.raises(ValueError, match="Unsupported lifetime"):
            provider.dispose_service(Services.GLOBAL_STATE)
