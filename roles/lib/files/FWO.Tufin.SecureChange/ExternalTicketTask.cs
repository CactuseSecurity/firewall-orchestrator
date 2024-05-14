using FWO.Api.Data;
using FWO.Tufin.SecureChange;

namespace FWO.Tufin.SecureChange
{


	public class ExternalAccessRequestTicketTask : RequestReqTask
	{

		private List<NetworkUser>? SourceUsers = [];
		private List<NetworkUser>? DestinationUsers = [];
		private List<NetworkObject> Sources = [];
		private List<NetworkService> Services = [];
		private List<NetworkObject> Destinations = [];

		// mockup:
		private string Action = "accept";
		//private string Reason = "reasoning ...";
		private string Logging = "Ja";

		private string EndDate = "";
		private string AppId = "APP-4711";
		private string ComDocumented = "false";
		private string TicketText = "";
				
		private TicketTaskType TaskType = TicketTaskType.AccessRequest;

		public string FillTaskTemplate(string taskTemplate)
		{			
			return TicketText.Replace("@@TASKS@@", taskTemplate)
				.Replace("@@USERS@@", SourceUsers.ToString())
				.Replace("@@SOURCES@@", Sources.ToString())
				.Replace("@@DESTINATIONS@@", Destinations.ToString())
				.Replace("@@SERVICES@@", Services.ToString())
				.Replace("@@ACTION@@", Action)
				.Replace("@@REASON@@", Reason)
				.Replace("@@LOGGING@@", Logging)
				.Replace("@@ENDDATE@@", EndDate)
				.Replace("@@APPID@@", AppId)
				.Replace("@@COM_DOCUMENTED@@", ComDocumented);
		
		}
		
		// TODO:  implement Users, Action, Reason, Logging, EndDate, AppId, ComDocumented

		public ExternalAccessRequestTicketTask(ModellingConnection conn)

		{
			// Sources = conn.SourceNwGroups.All();
			// Destinations = conn.DestinationNwGroups.All();
			// Services = conn.Services.All( _ => _);
		}

		public ExternalAccessRequestTicketTask(List<NetworkObject> sources, List<NetworkService> services, List<NetworkObject> destinations)

		{
			Sources = sources;
			Destinations = destinations;
			Services = services;
		}

	}
}

