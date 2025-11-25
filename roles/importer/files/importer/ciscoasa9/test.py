

from typing import Any
from pydantic import BaseModel, Field


class Test(BaseModel):
    model_config = {
        "populate_by_name": True
    }
    test_field: str = Field(description="A test field", alias="TestField", alias_priority=2)
    another_field: int = Field(description="Another test field")

    def model_dump(self, **kwargs: Any) -> "Test":
        kwargs.setdefault("by_alias", True)
        return super().model_dump(**kwargs)

test = Test(test_field="example", another_field=42)

e = test.test_field
print(test.model_dump())

    