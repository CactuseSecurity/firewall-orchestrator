using FWO.Data;

namespace FWO.Compliance
{
    public class SpecialZoneCalculator
    {
        private ComplianceNetworkZone _networkZone;

        public SpecialZoneCalculator(ComplianceNetworkZone networkZone)
        {
            _networkZone = networkZone;
        }

        public void CalculateInternetZone()
        {
            throw new NotImplementedException("Method not implemented");
        }

    }

}