using FWO.Config.Api;

namespace FWO.Ui.Display
{
    /// <summary>
    /// Html display of NAT rules. The translated part of a NAT rule is displayed by the methods inherited from <see cref="RuleDisplayHtml"/>.
    /// </summary>
    public class NatRuleDisplayHtml : RuleDisplayHtml
    {
        public NatRuleDisplayHtml(UserConfig userConfig) : base(userConfig)
        { }
    }
}
