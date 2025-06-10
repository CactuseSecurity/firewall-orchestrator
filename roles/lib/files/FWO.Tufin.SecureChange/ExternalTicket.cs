using FWO.Data;
using FWO.Data.Workflow;
using FWO.Data.Modelling;
using FWO.Logging;
using Newtonsoft.Json;
using RestSharp;
using System.Text.Json.Serialization; 

namespace FWO.Tufin.SecureChange
{
	public class ExternalTicket
	{
		[JsonProperty("ticketText"), JsonPropertyName("ticketText")]
		public string TicketText { get; set; } = "";

		public string? TicketId { get; set; } = "";
		protected List<string> TicketTasks = [];
		public ExternalTicketSystem TicketSystem { get; set; } = new();

		public ExternalTicket(){}

		public virtual Task CreateRequestString(List<WfReqTask> tasks, List<IpProtocol> ipProtos, ModellingNamingConvention? namingConvention)
		{
			throw new NotImplementedException();
		}

		public virtual string GetTaskTypeAsString(WfReqTask task)
		{
			return "";
		}

		public virtual Task<(string, string?)> GetNewState(string oldState)
		{
			throw new NotImplementedException();
		}

		public virtual Task<RestResponse<int>> CreateExternalTicket()
		{
			throw new NotImplementedException();
		}

		protected virtual Task<RestResponse<int>> PollExternalTicket()
		{
			throw new NotImplementedException();
		}

		protected static void CheckForProperJson(string jsonString)
		{
			try
			{
				System.Text.Json.JsonSerializer.Deserialize<object>(jsonString);
			}
			catch (Exception ex)
			{
				Log.WriteError("Check Json string: ", ex.ToString());
			}
		}
	}
}
