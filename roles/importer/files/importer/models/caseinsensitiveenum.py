from enum import Enum

class CaseInsensitiveEnum(str, Enum):
    @classmethod
    def from_str(cls, value: str):
        # Iterate through enum members and perform case-insensitive comparison
        for item in cls:
            if item.value.lower() == value.lower():
                return item
        raise ValueError(f"'{value}' is not a valid {cls.__name__}")

    @classmethod
    def __get_validators__(cls):
        yield cls.validate

    @classmethod
    def validate(cls, value):
        # This is used by Pydantic to validate the input during model creation
        if isinstance(value, str):
            return cls.from_str(value)
        raise TypeError(f"'{value}' is not a valid {cls.__name__}")
