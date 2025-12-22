from typing import Any

from fwo_api import FwoApi
from pytest_mock import MockerFixture, MockType


def mock_get_graphql_code(mocker: MockerFixture, return_value: Any) -> MockType:
    return mocker.patch.object(FwoApi, "get_graphql_code", return_value=return_value)


def mock_login(mocker: MockerFixture, return_value: Any) -> MockType:
    return mocker.patch.object(FwoApi, "login", return_value=return_value)
