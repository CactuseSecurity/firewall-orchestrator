using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class OwnerQueries : Queries
    {
        public static readonly string ownerDetailsFragment;

        public static readonly string getOwners;
        public static readonly string newOwner;
        public static readonly string updateOwner;
        public static readonly string deleteOwner;
        public static readonly string setDefaultOwner;


        static OwnerQueries()
        {
            try
            {
                ownerDetailsFragment = File.ReadAllText(QueryPath + "owner/fragments/ownerDetails.graphql");

                getOwners = ownerDetailsFragment + File.ReadAllText(QueryPath + "owner/getOwners.graphql");
                newOwner = File.ReadAllText(QueryPath + "owner/newOwner.graphql");
                updateOwner = File.ReadAllText(QueryPath + "owner/updateOwner.graphql");
                deleteOwner = File.ReadAllText(QueryPath + "owner/deleteOwner.graphql");
                setDefaultOwner = File.ReadAllText(QueryPath + "owner/setDefaultOwner.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize OwnerQueries", "Api OwnerQueries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}
