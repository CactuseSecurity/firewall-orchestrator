using FWO.Api.Data;

namespace FWO.Tufin.SecureChange
{
	public abstract class ExternalTicketTask
	{
		public readonly WfReqTask ReqTask = new();
		public string TaskText = "";
		public ModellingNamingConvention? NamingConvention;
		protected List<IpProtocol> IpProtos = [];


		public ExternalTicketTask(WfReqTask reqTask, List<IpProtocol> ipProtos, ModellingNamingConvention? namingConvention)
		{
			ReqTask = reqTask;
			IpProtos = ipProtos;
			NamingConvention = namingConvention;
		}

		public abstract void FillTaskText(string tasksTemplate);
	}
}
