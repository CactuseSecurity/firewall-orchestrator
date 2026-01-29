from typing import Any

from fwo_api import FwoApi
from pytest_mock import MockerFixture, MockType


def mock_get_graphql_code(mocker: MockerFixture, return_value: Any) -> MockType:
    return mocker.patch.object(FwoApi, "get_graphql_code", return_value=return_value)


def mock_login(mocker: MockerFixture, return_value: Any | None = None, side_effect: Any | None = None) -> MockType:
    assert not (return_value is not None and side_effect is not None), "Cannot set both return_value and side_effect"
    assert return_value is not None or side_effect is not None, "Either return_value or side_effect must be set"
    return mocker.patch.object(FwoApi, "login", return_value=return_value, side_effect=side_effect)
