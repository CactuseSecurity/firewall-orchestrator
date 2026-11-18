from __future__ import annotations

from typing import TYPE_CHECKING, Any

from fw_modules.fortiadom5ff import fwcommon
from fw_modules.fortiadom5ff.fwcommon import (
    add_global_policy_package_to_vdom,
    assign_case_all,
    assign_case_exclude,
    assign_case_include,
)

if TYPE_CHECKING:
    from pytest_mock import MockerFixture

ADOM = "adom1"
DEVICE = "fw1"
GLOBAL_PKG = "global_pkg"


def _structure() -> dict[str, dict[str, dict[str, Any]]]:
    return {
        ADOM: {
            DEVICE: {
                "root": {"local": "pkg_a", "global": ""},
                "vdom_b": {"local": "pkg_b", "global": ""},
                "vdom_no_pkg": "",
            }
        }
    }


def _assignment(assign_excluded: int, specify_assign_pkg_list: int, pkg_names: list[str]) -> dict[str, Any]:
    return {
        "assign_name": GLOBAL_PKG,
        "assign_excluded": assign_excluded,
        "specify_assign_pkg_list": specify_assign_pkg_list,
        "pkg list": [{"name": name} for name in pkg_names],
    }


def test_assign_case_all_skips_vdom_without_package() -> None:
    structure = _structure()

    assign_case_all(structure, ADOM, _assignment(0, 0, []))

    vdoms = structure[ADOM][DEVICE]
    assert vdoms["root"] == {"local": "pkg_a", "global": GLOBAL_PKG}
    assert vdoms["vdom_b"] == {"local": "pkg_b", "global": GLOBAL_PKG}
    assert vdoms["vdom_no_pkg"] == ""


def test_assign_case_include_skips_vdom_without_package() -> None:
    structure = _structure()

    assign_case_include(structure, ADOM, _assignment(0, 1, ["pkg_a"]))

    vdoms = structure[ADOM][DEVICE]
    assert vdoms["root"] == {"local": "pkg_a", "global": GLOBAL_PKG}
    assert vdoms["vdom_b"] == {"local": "pkg_b", "global": ""}
    assert vdoms["vdom_no_pkg"] == ""


def test_assign_case_exclude_skips_vdom_without_package() -> None:
    structure = _structure()

    assign_case_exclude(structure, ADOM, _assignment(1, 1, ["pkg_a"]))

    vdoms = structure[ADOM][DEVICE]
    assert vdoms["root"] == {"local": "pkg_a", "global": ""}
    assert vdoms["vdom_b"] == {"local": "pkg_b", "global": GLOBAL_PKG}
    assert vdoms["vdom_no_pkg"] == ""


def test_add_global_policy_package_to_vdom_with_vdom_without_package(mocker: MockerFixture) -> None:
    structure = _structure()
    api_call = mocker.patch.object(fwcommon.fmgr_getter, "fortinet_api_call", return_value=[_assignment(0, 0, [])])

    add_global_policy_package_to_vdom(structure, "sid", "https://fmgr/jsonrpc", ADOM)

    api_call.assert_called_once_with("sid", "https://fmgr/jsonrpc", f"/pm/config/adom/{ADOM}/_adom/options")
    vdoms = structure[ADOM][DEVICE]
    assert vdoms["root"]["global"] == GLOBAL_PKG
    assert vdoms["vdom_b"]["global"] == GLOBAL_PKG
    assert vdoms["vdom_no_pkg"] == ""
