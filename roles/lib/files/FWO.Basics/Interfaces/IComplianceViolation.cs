namespace FWO.Basics.Interfaces
{
    public interface IComplianceViolation
    {
        int RuleId { get; set; }
        DateTime FoundDate { get; set; }
        DateTime? RemovedDate { get; set; }
        string Details { get; set; }
        long RiskScore { get; set; }
        int PolicyId { get; set; }
        int CriterionId { get; set; }
    } 
}


