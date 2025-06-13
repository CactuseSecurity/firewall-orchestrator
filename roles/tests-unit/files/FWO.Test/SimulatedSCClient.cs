using FWO.Data;
using FWO.Services;
using FWO.Tufin.SecureChange;
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

		/// <summary>
		/// constructor
		/// </summary>
		public SimulatedSCClient(ExternalTicketSystem tufinSystem) : base(tufinSystem)
		{ }


        /// <summary>
        /// Simulated Rest call
        /// </summary>
        public override async Task<RestResponse<int>> RestCall(RestRequest request, string restEndPoint)
		{
			CallCounter++;
            await DefaultInit.DoNothing(); // qad avoid compiler warning
			if (CallCounter == 2)
			{
				return new(new()) { StatusCode = HttpStatusCode.BadRequest, Content = "{\"ticket\": {\"id\": 1, \"status\": \"In Progress\", \"Error\": \"FIELD_VALIDATION_ERROR\"} }" };
			}
            return new(new()) { StatusCode = HttpStatusCode.OK, Content = "{\"ticket\": {\"id\": 1, \"status\": \"In Progress\" } }" };
		}
	}
}
