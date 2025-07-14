using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Data.Workflow
{
    public class WfTicket : WfTicketBase
    {
        [JsonProperty("reqtasks"), JsonPropertyName("reqtasks")]
        public List<WfReqTask> Tasks { get; set; } = [];

        [JsonProperty("comments"), JsonPropertyName("comments")]
        public List<WfCommentDataHelper> Comments { get; set; } = [];

        public bool Editable { get; set; } = true;


        public WfTicket()
        { }

        public WfTicket(WfTicket ticket) : base(ticket)
        {
            Tasks = ticket.Tasks;
            Comments = ticket.Comments;
        }

        public int HighestTaskNumber()
        {
            int highestNumber = 0;
            foreach (var tasknumber in Tasks.Select(r => r.TaskNumber).Where(t => t > highestNumber))
            {
                highestNumber = tasknumber;
            }
            return highestNumber;
        }

        public int NumberImplTasks()
        {
            int numberImplTasks = 0;
            foreach (var reqtask in Tasks)
            {
                numberImplTasks += reqtask.ImplementationTasks.Count;
            }
            return numberImplTasks;
        }

        public void UpdateIpStringsFromCidrInTaskElements()
        {
            foreach (WfReqTask reqtask in Tasks)
            {
                foreach (WfReqElement elem in reqtask.Elements)
                {
                    elem.IpString = elem.Cidr != null && elem.Cidr.Valid ? elem.Cidr.CidrString : null;
                    elem.IpEnd = elem.CidrEnd != null && elem.CidrEnd.Valid ? elem.CidrEnd.CidrString : null;
                }
            }
        }

        public void UpdateCidrsInTaskElements()
        {
            foreach (WfReqTask reqtask in Tasks)
            {
                foreach (WfReqElement elem in reqtask.Elements)
                {
                    if (elem.IpString != null)
                    {
                        elem.Cidr = new Cidr(elem.IpString);
                    }
                    if (elem.IpEnd != null)
                    {
                        elem.CidrEnd = new Cidr(elem.IpEnd);
                    }
                }
                UpdateCidrsInImplTaskElements(reqtask.ImplementationTasks);
            }
        }

        public static void UpdateCidrsInImplTaskElements(List<WfImplTask> implementationTasks)
        {
            foreach (WfImplTask implTask in implementationTasks)
            {
                foreach (WfImplElement elem in implTask.ImplElements)
                {
                    if (elem.IpString != null)
                    {
                        elem.Cidr = new Cidr(elem.IpString);
                    }
                    if (elem.IpEnd != null)
                    {
                        elem.CidrEnd = new Cidr(elem.IpEnd);
                    }
                }
            }
        }

        public bool IsEditableForOwner(List<long> ticketIds, List<int> ownerIds, int requesterId)
        {
            return ticketIds.Contains(Id) || Tasks.Any(ta => ta.Owners.Any(ow => ownerIds.Contains(ow.Owner.Id))) || Requester?.DbId == requesterId;
        }

        public bool IsVisibleForOwner(List<long> ticketIds, List<int> ownerIds, int requesterId)
        {
            return IsEditableForOwner(ticketIds, ownerIds, requesterId) || Tasks.Any(ta => ownerIds.Contains(ta.GetAddInfoIntValueOrZero(AdditionalInfoKeys.ReqOwner)));
        }
    }
}
