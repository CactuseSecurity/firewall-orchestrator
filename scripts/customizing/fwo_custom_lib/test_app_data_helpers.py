import json
import logging
from pathlib import Path
from typing import TYPE_CHECKING

from netaddr import IPAddress

from scripts.customizing.fwo_custom_lib.app_data_basics import (
    build_owner_json_path,
    transform_app_list_to_dict,
    transform_owner_dict_to_list,
    write_owners_to_json,
)
from scripts.customizing.fwo_custom_lib.app_data_models import Appip, Owner
from scripts.customizing.fwo_custom_lib.basic_helpers import (
    FWOLogger,
    get_logger,
    read_custom_config,
    read_custom_config_with_default,
)

if TYPE_CHECKING:
    from _pytest.logging import LogCaptureFixture


def test_owner_and_appip_json_include_optional_fields() -> None:
    owner = Owner(
        name="Payments",
        app_id_external="APP-001",
        recert_period_days=182,
        days_until_first_recert=90,
        recert_active=True,
        import_source="csv",
        owner_lifecycle_state="active",
        criticality="high",
        responsibles={"1": ["CN=owner"]},
        additional_information={"costCenter": "1234"},
    )
    owner.app_servers.append(
        Appip(
            app_id_external="APP-001",
            ip_start=IPAddress("10.0.0.1"),
            ip_end=IPAddress("10.0.0.3"),
            ip_type="network",
            name="payments-net",
        )
    )

    owner_json = owner.to_json()

    assert owner_json["criticality"] == "high"
    assert owner_json["responsibles"] == {"1": ["CN=owner"]}
    assert owner_json["additional_information"] == {"costCenter": "1234"}
    assert owner_json["app_servers"] == [
        {
            "name": "payments-net",
            "app_id_external": "APP-001",
            "ip": "10.0.0.1",
            "ip_end": "10.0.0.3",
            "type": "network",
        }
    ]


def test_transform_helpers_and_write_owners_to_json(tmp_path: Path) -> None:
    owner = Owner("Payments", "APP-001", 365, 365)
    owner_dict = transform_app_list_to_dict([owner])
    script_path = tmp_path / "get_owner_data.py"

    assert owner_dict == {"APP-001": owner}
    assert build_owner_json_path(str(script_path)) == str(tmp_path / "get_owner_data.json")
    assert transform_owner_dict_to_list(owner_dict) == {"owners": [owner.to_json()]}

    output_path = write_owners_to_json(owner_dict, str(script_path), logger=logging.getLogger("app-data-test"))

    assert output_path == str(tmp_path / "get_owner_data.json")
    assert json.loads(Path(output_path).read_text(encoding="utf-8")) == {"owners": [owner.to_json()]}


def test_read_custom_config_accepts_comments_and_trailing_commas(tmp_path: Path) -> None:
    config_path = tmp_path / "customizing.json"
    config_path.write_text(
        """
        {
          // line comment
          "name": "value // preserved",
          "items": [
            "one",
          ],
          # hash comment
          "nested": {
            "enabled": true,
          },
          /* block comment */
        }
        """,
        encoding="utf-8",
    )
    logger = logging.getLogger("basic-helper-test")

    assert read_custom_config(str(config_path), "name", logger) == "value // preserved"
    assert read_custom_config_with_default(str(config_path), "missing", "fallback", logger) == "fallback"
    assert read_custom_config(str(config_path), "missing", logger) is None


def test_get_logger_configures_debug_helpers(caplog: "LogCaptureFixture") -> None:
    logger = get_logger(2)

    assert isinstance(logger, FWOLogger)
    assert logger.is_debug_level(2) is True
    assert logger.is_debug_level(3) is False

    with caplog.at_level(logging.DEBUG, logger="import-fworch-app-data"):
        logger.debug_if(2, "debug visible")
        logger.info_if(3, "info hidden")
        logger.warning_if(2, "warning visible")

    assert "debug visible" in caplog.text
    assert "warning visible" in caplog.text
    assert "info hidden" not in caplog.text
