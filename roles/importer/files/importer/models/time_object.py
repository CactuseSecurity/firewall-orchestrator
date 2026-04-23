from datetime import datetime, timezone

from pydantic import BaseModel, field_validator


class TimeObject(BaseModel):
    time_obj_uid: str
    time_obj_name: str
    start_time: str | None = None
    end_time: str | None = None

    # time format must be like '1970-01-01T00:00:00+00:00'.
    # needs to match format from database, otherwise changes will be detected for all time objects during import
    # we only write UTC times to the database, so we convert any timezone-aware time to UTC and store it in that format
    @field_validator("start_time", "end_time")
    @classmethod
    def validate_time_format(cls, value: str | None) -> str | None:
        if value is None:
            return value
        try:
            normalized_value = value.replace("Z", "+00:00")
            parsed_time = datetime.fromisoformat(normalized_value)
            if parsed_time.tzinfo is None:
                parsed_time = parsed_time.replace(tzinfo=timezone.utc)
            return parsed_time.astimezone(timezone.utc).isoformat(timespec="seconds")
        except ValueError:
            raise ValueError(f"Time value '{value}' does not match format 'YYYY-MM-DDTHH:MM:SS+HH:MM'") from None


class TimeObjectForImport(BaseModel):
    """
    TimeObject model containing all fields required for import into the database.
    """

    mgm_id: int
    time_obj_uid: str
    time_obj_name: str
    start_time: str | None = None
    end_time: str | None = None
    created: int

    @classmethod
    def from_normalized(cls, time_obj: TimeObject, mgm_id: int, import_id: int) -> "TimeObjectForImport":
        return cls(
            time_obj_uid=time_obj.time_obj_uid,
            time_obj_name=time_obj.time_obj_name,
            start_time=time_obj.start_time,
            end_time=time_obj.end_time,
            mgm_id=mgm_id,
            created=import_id,
        )
