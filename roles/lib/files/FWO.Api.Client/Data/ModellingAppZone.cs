using FWO.Api.Data;

namespace FWO.Api.Client.Data
{
    public class ModellingAppZone : ModellingAppRole
    {
        public ModellingAppZone()
        {
                
        }

        public ModellingAppZone(ModellingAppZone appZone) : base(appZone)
        {
            Comment = appZone.Comment;
            Creator = appZone.Creator;
            CreationDate = appZone.CreationDate;
            AppServers = appZone.AppServers;
            Area = appZone.Area;            
        }
    }
}
