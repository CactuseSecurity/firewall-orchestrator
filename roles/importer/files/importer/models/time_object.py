from pydantic import BaseModel


class TimeObject(BaseModel):
    time_obj_uid: str
    time_obj_name: str
    time_obj_type: str
    start_time: str | None = None
    end_time: str | None = None


class TimeObjectForImport(TimeObject):
    mgm_id: int
    created: int

    @classmethod
    def from_normalized(cls, time_obj: TimeObject, mgm_id: int, import_id: int) -> "TimeObjectForImport":
        return cls(
            time_obj_uid=time_obj.time_obj_uid,
            time_obj_name=time_obj.time_obj_name,
            time_obj_type=time_obj.time_obj_type,
            start_time=time_obj.start_time,
            end_time=time_obj.end_time,
            mgm_id=mgm_id,
            created=import_id,
        )
