from pydantic import BaseModel


class AdditionalCredential(BaseModel):
    id: int
    user: str = ""
    secret: str
