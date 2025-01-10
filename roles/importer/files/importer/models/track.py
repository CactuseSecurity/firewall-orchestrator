from pydantic import BaseModel


class Track(BaseModel):
	track_id: int
	track_name: str
	