import pytest

from fw_modules.fortiadom5ff import fmgr_getter
from fwo_exceptions import FwoImporterError


@pytest.mark.parametrize("domain_name", [None, ""])
def test_require_domain_name_raises_when_missing(domain_name: str | None):
    with pytest.raises(FwoImporterError):
        fmgr_getter.require_domain_name(domain_name, "unit-test")


def test_require_domain_name_returns_value():
    assert fmgr_getter.require_domain_name("root", "unit-test") == "root"
