from models.gateway import Gateway


class GatewayController(Gateway):  # noqa: PLW1641
    def __init__(self, gw: Gateway):
        self.Gateway = gw

    def __eq__(self, other: object) -> bool:
        if isinstance(other, Gateway):
            return (
                self.Name == other.Name
                and self.Uid == other.Uid
                and self.Routing == other.Routing
                and self.Interfaces == other.Interfaces
                and self.RulebaseLinks == other.RulebaseLinks
                and self.EnforcedNatPolicyUids == other.EnforcedNatPolicyUids
                and self.EnforcedPolicyUids == other.EnforcedPolicyUids
                and self.GlobalPolicyUid == other.GlobalPolicyUid
                and self.ImportDisabled == other.ImportDisabled
                and self.ShowInUI == other.ShowInUI
            )
        return NotImplemented
