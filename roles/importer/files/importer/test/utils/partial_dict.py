from typing import Any, cast


class PartialDict(dict[str, Any]):  # noqa: PLW1641
    def __eq__(self, other: object) -> bool:
        try:
            if not isinstance(other, dict):
                return False

            # Checks if this dict's keys/values are a subset of 'other'
            other_dict: dict[str, Any] = cast("dict[str, Any]", other)
            return all(other_dict.get(k) == v for k, v in self.items())
        except Exception:
            return False
