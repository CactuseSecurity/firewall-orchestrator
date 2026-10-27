using System.Text.Json.Serialization;
using FWO.Basics;
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
                    elem.IpString = elem.Cidr != null && elem.Cidr.Valid ? elem.Cidr.CidrString.StripOffUnnecessaryNetmask() : null;
                    elem.IpEnd = elem.CidrEnd != null && elem.CidrEnd.Valid ? elem.CidrEnd.CidrString.StripOffUnnecessaryNetmask() : null;
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

        public void ResetStateChangeTracking()
        {
            ResetStateChanged();
            foreach (WfReqTask reqTask in Tasks)
            {
                reqTask.ResetStateChanged();
                foreach (WfApproval approval in reqTask.Approvals)
                {
                    approval.ResetStateChanged();
                }
                foreach (WfImplTask implTask in reqTask.ImplementationTasks)
                {
                    implTask.ResetStateChanged();
                }
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

        /// <summary>
        /// Checks whether the owners may change the ticket: as requester, as app the ticket was registered for or as owner of a
        /// request task (the requested app of an interface request). The requesting app of an interface request may only change
        /// it where requested, i.e. in the request phase.
        /// </summary>
        /// <param name="ticketIds">Ids of the tickets registered for the owners.</param>
        /// <param name="ownerIds">Ids of the owners.</param>
        /// <param name="requesterId">Id of the user, matched against the requester of the ticket.</param>
        /// <param name="includeRequestingOwner">True if the requesting app of an interface request may change it.</param>
        public bool IsEditableForOwner(List<long> ticketIds, List<int> ownerIds, int requesterId, bool includeRequestingOwner = false)
        {
            return ticketIds.Contains(Id) || Tasks.Any(ta => ta.Owners.Any(ow => ownerIds.Contains(ow.Owner.Id))) || Requester?.DbId == requesterId
                || (includeRequestingOwner && IsRequestedByOwner(ownerIds));
        }

        /// <summary>
        /// Checks whether the owners may see the ticket: if they may change it or requested it as requesting app of an interface request.
        /// </summary>
        public bool IsVisibleForOwner(List<long> ticketIds, List<int> ownerIds, int requesterId)
        {
            return IsEditableForOwner(ticketIds, ownerIds, requesterId, true);
        }

        private bool IsRequestedByOwner(List<int> ownerIds)
        {
            return Tasks.Any(ta => ownerIds.Contains(ta.GetAddInfoIntValueOrZero(AdditionalInfoKeys.ReqOwner)));
        }
    }
}
