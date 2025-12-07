from models.gateway import Gateway

class GatewayController(Gateway):

    def __init__(self, gw: Gateway):
        self.Gateway = gw
     
    @staticmethod
    def replace_none_with_empty(s: str | None) -> str:
        if s is None or s == '':
            return '<EMPTY>'
        else:
            return str(s)

    def __eq__(self, other: object) -> bool:
        if isinstance(other, Gateway):
            return (
                self.Name == other.Name and
                self.Uid == other.Uid and
                self.Routing == other.Routing and
                self.Interfaces == other.Interfaces and
                self.RulebaseLinks == other.RulebaseLinks and
                self.EnforcedNatPolicyUids == other.EnforcedNatPolicyUids and
                self.EnforcedPolicyUids == other.EnforcedPolicyUids and
                self.GlobalPolicyUid == other.GlobalPolicyUid and
                self.ImportDisabled == other.ImportDisabled and
                self.ShowInUI == other.ShowInUI
            )
        return NotImplemented
