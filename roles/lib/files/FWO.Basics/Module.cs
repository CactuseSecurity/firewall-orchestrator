namespace FWO.Basics
{
    public enum Module
    {
        Report = 1,
        Workflow = 2,
        Recertification = 3,
        Modelling = 4,
        NetworkAnalysis = 5,
        Compliance = 6
    }

    public static class ModuleGroups
    {
        public static string AllModulesNumList()
        {
            return $"[{string.Join(",", Enum.GetValues(typeof(Module)).Cast<Module>().Select(m => (int)m).ToList())}]";
        }
    }
}
