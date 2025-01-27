using FWO.Api.Data;
using System.Text.Json.Serialization; 
using Newtonsoft.Json;
using RestSharp;
using RestSharp.Serializers.NewtonsoftJson;
using RestSharp.Serializers;
using FWO.Logging;

namespace FWO.Tufin.SecureChange
{
	public class ExternalTicket
	{
		[JsonProperty("ticketText"), JsonPropertyName("ticketText")]
		public string TicketText { get; set; } = "";

		public string? TicketId { get; set; } = "";
		protected List<string> TicketTasks = [];
		public ExternalTicketSystem TicketSystem = new();

		public ExternalTicket(){}

		public virtual Task CreateRequestString(List<WfReqTask> tasks, List<IpProtocol> ipProtos, ModellingNamingConvention? namingConvention)
		{
			throw new NotImplementedException();
		}

		public virtual string GetTaskTypeAsString(WfReqTask task)
		{
			return "";
		}

		public virtual async Task<(string, string?)> GetNewState(string oldState)
		{
			return ("","");
		}

		public async Task<RestResponse<int>> CreateExternalTicket()
		{
			string restEndPoint = "tickets.json";
			RestRequest request = new(restEndPoint, Method.Post);
			request.AddJsonBody(TicketText);

			// https://192.168.1.1/securechangeworkflow/api/securechange/tickets
			return await RestCall(request, restEndPoint);
		}

		protected async Task<RestResponse<int>> PollExternalTicket()
		{
			if(TicketId != null)
			{
				string restEndPoint = "tickets/" + TicketId;
				RestRequest request = new(restEndPoint, Method.Get);
				return await RestCall(request, restEndPoint);
			}
			throw new Exception("No Ticket Id given.");
		}

		protected async Task<RestResponse<int>> RestCall(RestRequest request, string restEndPoint)
		{
			request.AddHeader("Content-Type", "application/json");
			request.AddHeader("Authorization", TicketSystem.Authorization);
			request.AddHeader("Accept", "application/json");
			RestClientOptions restClientOptions = new();
			restClientOptions.RemoteCertificateValidationCallback += (_, _, _, _) => true;
			restClientOptions.BaseUrl = new Uri(TicketSystem.Url);
			RestClient restClient = new(restClientOptions, null, ConfigureRestClientSerialization);

			// Debugging SecureChange API call
			DebugApiCall(request, restClient, restEndPoint);

			// send API call
			return await restClient.ExecuteAsync<int>(request);
		}

		protected static void CheckForProperJson(string jsonString)
		{
			try
			{
				var tmpObj = System.Text.Json.JsonSerializer.Deserialize<object>(jsonString);
			}
			catch (Exception ex)
			{
				Log.WriteError("Check Json string: ", ex.ToString());
			}
		}

		private void ConfigureRestClientSerialization(SerializerConfig config)
		{
			JsonNetSerializer serializer = new (); // Case insensivitive is enabled by default
			config.UseSerializer(() => serializer);
		}

		private static void DebugApiCall(RestRequest request, RestClient restClient, string restEndPoint)
		{
			string headers = "";
			string body = "";
			foreach (Parameter p in request.Parameters)
			{
				if (p.Name == "")
				{
					body = $"data: '{p.Value}'";
				}
				else
				{
					if (p.Name != "Authorization") // avoid logging of credentials
						headers += $"header: '{p.Name}: {p.Value}' ";
				}
			}
			Log.WriteDebug("API", $"Sending API Call to SecureChange:\nrequest: {request.Method}, base url: {restClient.Options.BaseUrl}, restEndpoint: {restEndPoint}, body: {body}, {headers} ");
		}
	}
}
