using FWO.Api.Data;

namespace FWO.Tufin.SecureChange
{

	public class ExternalAccessRequestTicketTask : RequestReqTask
	{
		private ModellingConnection Connection = new();

		// mockup:
		private string Action = "Accept";
		private string Logging = "Ja";
		private string EndDate = "";
		// private string AppId = "APP-4711";
		//private string Reason = "der Grund ..."
		private string ComDocumented = "false";
				
		private TicketTaskType TaskType = TicketTaskType.AccessRequest;

		public ExternalAccessRequestTicketTask()
		{
		}

		public ExternalAccessRequestTicketTask(ModellingConnection modellingConnection)
		{
			Connection = modellingConnection;
		}


		public string FillTaskTemplate(string tasksTemplate)
		{			
			return tasksTemplate
				.Replace("@@USERS@@", "[\"Any\"]") // data not provided yet
				.Replace("@@SOURCES@@", ConvertNetworkObjectWrapperssToTufinJsonString(Connection.SourceAppServers))
				.Replace("@@DESTINATIONS@@", ConvertNetworkObjectWrapperssToTufinJsonString(Connection.SourceAppServers))
				.Replace("@@SERVICES@@", ConvertNetworkServiceWrapperssToTufinJsonString(Connection.Services))
				.Replace("@@ACTION@@", Action)
				.Replace("@@REASON@@", Reason)
				.Replace("@@LOGGING@@", Logging)
				.Replace("@@ENDDATE@@", EndDate)
				.Replace("@@APPID@@", Connection.App.ExtAppId)
				.Replace("@@COM_DOCUMENTED@@", ComDocumented);
		}

		static private string ConvertNetworkObjectWrapperssToTufinJsonString(List<ModellingAppServerWrapper> nwObjects)
		{
			// TODO: this is just a mock-up, needs to handle app roles, ...
			string result = "[";
			foreach (ModellingAppServerWrapper srv in nwObjects)
			{
				result += $@"{{
									""@type"": ""IP"",
									""ip_address"": ""{srv.Content.Ip}"",
									""netmask"": ""255.255.255.255"",
									""cidr"": 32
							}},";
			}
			result = result.TrimEnd(',');
			result += "]";
			return result;
		}

		static private string ConvertNetworkServiceWrapperssToTufinJsonString(List<ModellingServiceWrapper> services)
		{
			string result = "[";
			foreach (ModellingServiceWrapper svc in services)
			{
				result += $@"
				{{
					""@type"": ""PROTOCOL"", 
					""protocol"": ""{svc.Content.ProtoId}"", 
					""port"": {svc.Content.Port},
					""name"": ""{svc.Content.Name}""
				}},";
			}
			result = result.TrimEnd(',');
			result += "]";
			return result;
		}

	}
}
