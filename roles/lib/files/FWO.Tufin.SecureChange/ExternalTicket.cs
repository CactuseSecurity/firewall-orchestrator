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

		public virtual void CreateRequestString(List<WfReqTask> tasks)
		{}

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
			RestRequest request = new("tickets.json", Method.Post);
			request.AddJsonBody(TicketText);

			// https://192.168.1.1/securechangeworkflow/api/securechange/tickets
			return await RestCall(request, TicketSystem.Url);
		}

		protected async Task<RestResponse<int>> PollExternalTicket(string url)
		{
			if(TicketId != null)
			{
				RestRequest request = new("tickets.json", Method.Get);
				return await RestCall(request, url);
			}
			throw new Exception("No Ticket Id given.");
		}

		private async Task<RestResponse<int>> RestCall(RestRequest request, string url)
		{
			request.AddHeader("Content-Type", "application/json");
			request.AddHeader("Authorization", TicketSystem.Authorization);
			RestClientOptions restClientOptions = new();
			restClientOptions.RemoteCertificateValidationCallback += (_, _, _, _) => true;
			restClientOptions.BaseUrl = new Uri(url);
			RestClient restClient = new(restClientOptions, null, ConfigureRestClientSerialization);

			// Debugging SecureChange API call
			DebugApiCall(request, restClient);

			// send API call
			return await restClient.ExecuteAsync<int>(request);
		}

		protected void CheckForProperJson(string jsonString)
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

		private static void DebugApiCall(RestRequest request, RestClient restClient)
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
			Log.WriteDebug("API", $"Sending API Call to SecureChange:\nrequest: {request.Method}, url: {restClient.Options.BaseUrl}, {body}, {headers} ");
		}
	}
}
