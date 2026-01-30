from enum import Enum


class CaseInsensitiveEnum(str, Enum):
    @classmethod
    def _missing_(cls, value: object) -> object | None:
        if isinstance(value, str):
            s = value.strip()
            for member in cls:
                # match either the value or the name, case-insensitive
                if s.lower() in (member.value.lower(), member.name.lower()):
                    return member
        # returning None -> Enum will raise the usual ValueError
        return None
