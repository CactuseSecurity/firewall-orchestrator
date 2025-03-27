namespace FWO.Data.Modelling
{
    public class RuleRecognitionOption
    {
        public bool NwRegardIp { get; set; } = true;
        public bool NwRegardName { get; set; } = false;
        public bool NwRegardGroupName { get; set; } = false;
        public bool NwResolveGroup { get; set; } = false;

        public bool SvcRegardPortAndProt { get; set; } = true;
        public bool SvcRegardName { get; set; } = false;
        public bool SvcRegardGroupName { get; set; } = false;
        public bool SvcResolveGroup { get; set; } = true;
    }
}
