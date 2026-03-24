import pytest
from fw_modules.fortiadom5ff.fwcommon import to_time_object
from fwo_exceptions import ImportInterruptionError
from pytest_mock import MockerFixture


class TestToTimeObject:
    def test_to_time_object_parses_list_timestamps(self):
        time_obj = to_time_object(
            {
                "name": "work-hours",
                "start": ["12:00", "2026/02/17"],
                "end": ["18:30", "2026/02/17"],
            }
        )

        assert time_obj.time_obj_uid == "work-hours"
        assert time_obj.time_obj_name == "work-hours"
        assert time_obj.start_time == "2026-02-17T12:00:00"
        assert time_obj.end_time == "2026-02-17T18:30:00"

    def test_to_time_object_parses_single_string_timestamp(self):
        time_obj = to_time_object(
            {
                "name": "legacy-format",
                "start": "00:00 2020/01/01",
                "end": "23:59 2020/01/01",
            }
        )

        assert time_obj.start_time == "2020-01-01T00:00:00"
        assert time_obj.end_time == "2020-01-01T23:59:00"

    def test_to_time_object_returns_none_for_default_start_time(self):
        time_obj = to_time_object(
            {
                "name": "all-day",
                "start": "00:00",
                "end": None,
            }
        )

        assert time_obj.start_time is None
        assert time_obj.end_time is None

    def test_to_time_object_logs_warning_for_unsupported_time_only_format(self, mocker: MockerFixture):
        warning_mock = mocker.patch("fwo_log.FWOLogger.warning")

        time_obj = to_time_object(
            {
                "name": "unsupported",
                "start": "12:00",
                "end": "15:00",
            }
        )

        assert time_obj.start_time is None
        assert time_obj.end_time is None
        assert warning_mock.call_count == 2

    def test_to_time_object_logs_warning_for_invalid_datetime(self, mocker: MockerFixture):
        warning_mock = mocker.patch("fwo_log.FWOLogger.warning")

        time_obj = to_time_object(
            {
                "name": "broken-date",
                "start": ["12:00", "2026/13/17"],
                "end": ["99:99", "2026/02/17"],
            }
        )

        assert time_obj.start_time is None
        assert time_obj.end_time is None
        assert warning_mock.call_count == 2

    @pytest.mark.parametrize("missing_name", [None, ""])
    def test_to_time_object_raises_on_missing_name(self, missing_name: str | None):
        with pytest.raises(ImportInterruptionError):
            to_time_object(
                {
                    "name": missing_name,
                    "start": ["12:00", "2026/02/17"],
                    "end": ["18:00", "2026/02/17"],
                }
            )
