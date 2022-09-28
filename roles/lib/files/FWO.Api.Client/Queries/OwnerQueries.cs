using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class OwnerQueries : Queries
    {
        public static readonly string ownerDetailsFragment;

        public static readonly string getOwners;


        static OwnerQueries()
        {
            try
            {
                ownerDetailsFragment = File.ReadAllText(QueryPath + "owner/fragments/ownerDetails.graphql");
                getOwners = ownerDetailsFragment + File.ReadAllText(QueryPath + "owner/getOwners.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize OwnerQueries", "Api OwnerQueries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}
