namespace FWO.Data.Report
{
    public class RulebaseHelper : IComparable<RulebaseHelper>
    {
        public List<int> Parts { get; }
        public int Level
        {
            get
            {
                return Parts.Count;
            }
        }

        public RulebaseHelper(string versionString)
        {
            if(string.IsNullOrWhiteSpace(versionString))
                throw new ArgumentException("Version string cannot be null or empty.");

            Parts = [.. versionString
                .Split('.')
                .Select(p => int.TryParse(p, out int n) ? n : throw new FormatException("Invalid version part: " + p))];
        }

        public static int GetLevel(string versionString) => new RulebaseHelper(versionString).Parts.Count;

        public int CompareTo(RulebaseHelper other)
        {
            int maxLength = Math.Max(Parts.Count, other.Parts.Count);

            for(int i = 0; i < maxLength; i++)
            {
                int a = i < Parts.Count ? Parts[i] : 0;
                int b = i < other.Parts.Count ? other.Parts[i] : 0;

                if(a != b)
                    return a.CompareTo(b);
            }

            return 0;
        }

        public override string ToString() => string.Join(".", Parts);

        public override bool Equals(object obj)
        {
            return obj is RulebaseHelper other && CompareTo(other) == 0;
        }

        public override int GetHashCode() => string.Join(".", Parts).GetHashCode();

        public RulebaseHelper GetPrefix(int count)
        {
            if(count <= 0)
                throw new ArgumentException("Count must be positive.");

            List<int>? prefixParts = Parts.Take(count).ToList();
            return new RulebaseHelper(string.Join(".", prefixParts));
        }

        public bool EqualsUpTo(RulebaseHelper other, int partsToCompare)
        {
            for(int i = 0; i < partsToCompare; i++)
            {
                int a = i < Parts.Count ? Parts[i] : 0;
                int b = i < other.Parts.Count ? other.Parts[i] : 0;
                if(a != b)
                    return false;
            }
            return true;
        }

        //public static IEnumerable<IGrouping<string, Rule>> GroupByLevel(IEnumerable<Rule> objects)
        //{
        //    return objects.GroupBy(obj =>
        //    {
        //        RulebaseHelper? version = new(obj.DisplayOrderNumberString);
        //        return version.GetPrefix(version.Level - 1).ToString();
        //    });
        //}

        public static List<RuleGroup> GroupByLevel(IEnumerable<Rule> objects)
        {
            return objects.GroupBy(obj =>
            {
                RulebaseHelper? version = new(obj.DisplayOrderNumberString);
                return version.GetPrefix(version.Level - 1).ToString();
            })
            .Select(g => new RuleGroup
            {
                GroupName = g.Key,
                Rules = g.ToList()
            }).ToList();
        }

        public static bool operator >(RulebaseHelper a, RulebaseHelper b) => a.CompareTo(b) > 0;
        public static bool operator <(RulebaseHelper a, RulebaseHelper b) => a.CompareTo(b) < 0;
        public static bool operator ==(RulebaseHelper a, RulebaseHelper b) => a?.Equals(b) ?? b is null;
        public static bool operator !=(RulebaseHelper a, RulebaseHelper b) => !(a == b);
    }
}
