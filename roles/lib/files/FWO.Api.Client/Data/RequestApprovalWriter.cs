using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RequestApprovalWriter : RequestApprovalBase
    {
        public RequestApprovalWriter()
        { }

        public RequestApprovalWriter(RequestApproval approval) : base(approval)
        { 
        }
    }
}
