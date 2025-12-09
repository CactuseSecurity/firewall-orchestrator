from pydantic import BaseModel, ConfigDict
from pydantic.alias_generators import to_camel


class Voice(BaseModel):
    model_config = ConfigDict(alias_generator=to_camel, validate_by_name=True)

    name: str
    language_code: str


voice = Voice(name="Filiz", language_code="tr-TR")
print(voice.language_code)
# > tr-TR
print(voice.model_dump(by_alias=True))
