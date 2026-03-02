from collections.abc import Generator

from fw_modules.fortiosmanagementREST.fos_models import FortiOSConfig


def normalize_zone_name(zone_name: str) -> str:
    """
    Normalize a zone name.

    Args:
        zone_name (str): The zone name.

    Returns:
        str: The normalized zone name.

    """
    if zone_name == "any":
        return "global"
    return zone_name


def collect_zones(native_config: FortiOSConfig) -> Generator[str]:
    """
    Normalize zone objects from the native FortiOS configuration.

    Args:
        native_config (FortiOSConfig): The native FortiOS configuration.

    Yields:
        str: The zone name.

    """
    seen_zones: set[str] = set()

    def record_and_yield_zone(zone_name: str) -> Generator[str]:
        zone_name = normalize_zone_name(zone_name)
        if zone_name not in seen_zones:
            yield zone_name
            seen_zones.add(zone_name)

    # zones from network objects with associated interfaces
    for obj_with_intf in native_config.nw_obj_address + native_config.nw_obj_ippool:
        if not obj_with_intf.associated_interface:
            continue
        yield from record_and_yield_zone(obj_with_intf.associated_interface)

    # zones from rules
    for rule in native_config.rules:
        for intf_ref in rule.srcintf + rule.dstintf:
            yield from record_and_yield_zone(intf_ref.name)

    yield from record_and_yield_zone("global")  # ensure "global" zone exists
