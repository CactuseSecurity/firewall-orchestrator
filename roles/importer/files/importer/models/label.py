from __future__ import annotations

from pydantic import BaseModel


# LabelNormalized is the model for an imported key/value label (e.g. a Guardicore label), stored in labelling.label_*
class LabelNormalized(BaseModel):
    key_name: str
    value: str
