using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class RequestQueries : Queries
    {
        public static readonly string getTickets;
        public static readonly string newTicket;
        public static readonly string updateTicket;


        static RequestQueries()
        {
            try
            {
                getTickets = File.ReadAllText(QueryPath + "request/getTickets.graphql");
                newTicket = File.ReadAllText(QueryPath + "request/newTicket.graphql");
                updateTicket = File.ReadAllText(QueryPath + "request/updateTicket.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize RequestQueries", "Api RequestQueries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}
