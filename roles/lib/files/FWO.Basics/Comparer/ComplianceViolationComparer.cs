using FWO.Basics.Interfaces;

namespace FWO.Basics.Comparer
{
    public class ComplianceViolationComparer : IEqualityComparer<IComplianceViolation>
    {
        public bool Equals(IComplianceViolation? x, IComplianceViolation? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;

            return x.RuleId == y.RuleId &&
                x.PolicyId == y.PolicyId &&
                x.CriterionId == y.CriterionId &&
                x.Details == y.Details;
        }

        public int GetHashCode(IComplianceViolation obj)
        {
            return HashCode.Combine(obj.RuleId, obj.PolicyId, obj.CriterionId, obj.Details);
        }
    }
}