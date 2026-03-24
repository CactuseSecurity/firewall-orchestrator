namespace FWO.Basics
{
    public enum DailyCheckModule
    {
        DemoData = 1,
        Imports = 2,
        RecertRefresh = 3,
        RecertCheck = 4,
        UnansweredInterfaceRequests = 5,
        RuleExpiryCheck = 6,
        OwnerActiveRules = 7
    }

    public static class DailyCheckModuleGroups
    {
        public static string AllModulesNumList()
        {
            return $"[{string.Join(",", Enum.GetValues(typeof(DailyCheckModule)).Cast<DailyCheckModule>().Select(m => (int)m).ToList())}]";
        }
    }
}
