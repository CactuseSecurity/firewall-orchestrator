using FWO.Api.Data;

namespace FWO.Tufin.SecureChange
{

	public class ExternalAccessRequestTicketTask : RequestReqTask
	{
		private ModellingConnection Connection = new();

		// mockup:
		private string Action = "accept";
		private string Logging = "Ja";

		private string EndDate = "";
		// private string AppId = "APP-4711";
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
				.Replace("@@USERS@@", "[]") // data not provided yet
				.Replace("@@SOURCES@@", ConvertNetworkObjectWrapperssToTufinJsonString(Connection.SourceAppServers, "source"))
				.Replace("@@DESTINATIONS@@", ConvertNetworkObjectWrapperssToTufinJsonString(Connection.SourceAppServers, "destination"))
				.Replace("@@SERVICES@@", ConvertNetworkServiceWrapperssToTufinJsonString(Connection.Services))
				.Replace("@@ACTION@@", Action)
				.Replace("@@REASON@@", Reason)
				.Replace("@@LOGGING@@", Logging)
				.Replace("@@ENDDATE@@", EndDate)
				.Replace("@@APPID@@", Connection.App.ExtAppId)
				.Replace("@@COM_DOCUMENTED@@", ComDocumented);
		}

		private string ConvertNetworkObjectWrapperssToTufinJsonString(List<ModellingAppServerWrapper> nwObjects, string nwObjField = "source")
		{
			string result = "[]";
			// TODO: implement
			return result;
		}

		private string ConvertNetworkServiceWrapperssToTufinJsonString(List<ModellingServiceWrapper> services)
		{
			string result = "[";
			foreach (ModellingServiceWrapper svc in services)
			{
				result += $@"
				{{
					""@type"": ""PREDEFINED"", 
					""protocol"": ""{svc.Content.ProtoId}"", 
					""port"": {svc.Content.Port},
					""predefined_name"": ""{svc.Content.Name}""
				}},";
			}
			result = result.TrimEnd(',');
			result += "]";
			return result;
		}

	}
}
