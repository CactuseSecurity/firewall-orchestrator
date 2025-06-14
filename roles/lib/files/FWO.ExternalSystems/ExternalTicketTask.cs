using FWO.Data;
using FWO.Data.Workflow;
using FWO.Data.Modelling;

namespace FWO.ExternalSystems
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

		public abstract void FillTaskText(ExternalTicketTemplate template);
	}
}
