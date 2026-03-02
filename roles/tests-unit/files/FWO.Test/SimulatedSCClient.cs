using FWO.Data;
using FWO.Services;
using FWO.ExternalSystems.Tufin.SecureChange;
using System.Net;
using RestSharp;

namespace FWO.Test
{
    /// <summary>
    /// SC Ticket with simulated Rest call
    /// </summary>
    public class SimulatedSCClient : SCClient
    {
        private int CallCounter = 0;
        private readonly Dictionary<string, Queue<RestResponse<int>>> queuedResponses = new();

        /// <summary>
        /// constructor
        /// </summary>
        public SimulatedSCClient(ExternalTicketSystem tufinSystem) : base(tufinSystem)
        { }

        public void EnqueueResponse(string restEndPoint, RestResponse<int> response)
        {
            if (!queuedResponses.TryGetValue(restEndPoint, out Queue<RestResponse<int>>? responses))
            {
                responses = new Queue<RestResponse<int>>();
                queuedResponses[restEndPoint] = responses;
            }
            responses.Enqueue(response);
        }

        /// <summary>
        /// Simulated Rest call
        /// </summary>
        public override async Task<RestResponse<int>> RestCall(RestRequest request, string restEndPoint)
        {
            CallCounter++;
            await DefaultInit.DoNothing(); // qad avoid compiler warning
            if (queuedResponses.TryGetValue(restEndPoint, out Queue<RestResponse<int>>? responses) && responses.Count > 0)
            {
                return responses.Dequeue();
            }
            if (CallCounter == 2)
            {
                return new(new()) { StatusCode = HttpStatusCode.BadRequest, Content = "{\"ticket\": {\"id\": 1, \"status\": \"In Progress\", \"Error\": \"FIELD_VALIDATION_ERROR\"} }" };
            }
            return new(new()) { StatusCode = HttpStatusCode.OK, Content = "{\"ticket\": {\"id\": 1, \"status\": \"In Progress\" } }" };
        }
    }
}
