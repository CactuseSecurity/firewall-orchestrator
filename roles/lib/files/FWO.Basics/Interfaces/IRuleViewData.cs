namespace FWO.Basics.Interfaces
{
    public interface IRuleViewData
    {
        string MgmtId { get; set; }
        string Uid { get; set; }
        string Name { get; set; }
        string Source { get; set; }
        string Destination { get; set; }
        string Services { get; set; }
        string Action { get; set; }
        string InstallOn { get; set; }
        string Compliance { get; set; }
        string ViolationDetails { get; set; }
        string ChangeID { get; set; }
        string AdoITID { get; set; }
        string Comment { get; set; }
        string RulebaseId { get; set; }
        string Disabled { get; set; }
    }   
}

