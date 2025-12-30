__version__ = "2025-11-20-01"
# revision history:
# 2025-11-20-01, initial version

from netaddr import IPAddress


class Owner:
    def __init__(self, name, app_id_external, main_user, 
                 recert_period_days, days_until_first_recert, recert_active=False, 
                 import_source="defaultSource"):
        self.name: str = name
        self.app_id_external: str = app_id_external
        self.main_user: str = main_user
        self.modellers: list[str] = []
        self.import_source: str = import_source
        self.app_servers: list[Appip] = []
        self.recert_active: bool = recert_active
        self.recert_period_days: int = recert_period_days
        self.days_until_first_recert: int = days_until_first_recert

    def to_json(self):
        return (
            {
                "name": self.name,
                "app_id_external": self.app_id_external,
                "main_user": self.main_user,
                "import_source": self.import_source,
                "app_servers": [ip.to_json() for ip in self.app_servers],
                "recert_active": self.recert_active,
                "recert_period_days": self.recert_period_days,
                "days_until_first_recert": self.days_until_first_recert
            }
        )


class Appip:
    def __init__(self, app_id_external: str, ip_start: IPAddress, ip_end: IPAddress, type: str, name: str):
        self.name: str = name
        self.app_id_external: str = app_id_external
        self.ip_start = ip_start
        self.ip_end = ip_end
        self.type: str = type

    def to_json(self):
        return (
            {
            "name": self.name,
            "app_id_external": self.app_id_external,
            "ip": str(IPAddress(self.ip_start)),
            "ip_end": str(IPAddress(self.ip_end)),
            "type": self.type
            }
        )
