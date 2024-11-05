from typing import Dict
from models.rule import Rule
from pydantic import BaseModel

"""
'policy':
    {
        'policy_name': 'pol1',
        'policy_uid': 'a32bc348234-23432a',
        'rules': [ { ... }, { ... }, ... ]
    }
"""
class Policy(BaseModel):
    Uid: str
    Name: str
    Rules: Dict[str, Rule] = {}
    # EnforcingGatewayUids: List[str]
