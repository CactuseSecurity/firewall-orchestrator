from pydantic import BaseModel


class TimeObject(BaseModel):
    time_obj_uid: str
    time_obj_name: str
    time_obj_type: str
    start_time: str | None = None
    end_time: str | None = None
