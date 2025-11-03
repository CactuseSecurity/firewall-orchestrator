from pydantic import BaseModel


class Action(BaseModel):
	action_id: int
	action_name: str
	allowed: bool = True
