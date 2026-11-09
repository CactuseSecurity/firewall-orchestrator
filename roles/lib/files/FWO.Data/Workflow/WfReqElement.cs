using System.Text.Json.Serialization;
using FWO.Basics;
using NetTools;
using Newtonsoft.Json;

namespace FWO.Data.Workflow
{
    public class WfReqElement : WfElementBase
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonProperty("task_id"), JsonPropertyName("task_id")]
        public long TaskId { get; set; }

        [JsonProperty("request_action"), JsonPropertyName("request_action")]
        public string RequestAction { get; set; } = Workflow.RequestAction.create.ToString();

        [JsonProperty("device_id"), JsonPropertyName("device_id")]
        public int? DeviceId { get; set; }

        public Cidr? Cidr { get; set; } = new();
        public Cidr? CidrEnd { get; set; } = new();


        public WfReqElement()
        { }

        public WfReqElement(WfReqElement element) : base(element)
        {
            Id = element.Id;
            TaskId = element.TaskId;
            RequestAction = element.RequestAction;
            DeviceId = element.DeviceId;
            Cidr = element.Cidr;
            CidrEnd = element.CidrEnd;
        }

        public bool IsMemberToAdd()
        {
            return RequestAction == Workflow.RequestAction.create.ToString()
                || RequestAction == Workflow.RequestAction.addAfterCreation.ToString();
        }

        public bool IsMemberToRemove()
        {
            return RequestAction == Workflow.RequestAction.delete.ToString();
        }

        public bool IsCurrentMember()
        {
            return !IsMemberToAdd() && !IsMemberToRemove();
        }

        /// <summary>
        /// Returns the address range of the element, IPv4 or IPv6: from <see cref="WfElementBase.IpString"/> and
        /// <see cref="WfElementBase.IpEnd"/>, or from <see cref="Cidr"/> and <see cref="CidrEnd"/> when only those are set.
        /// Null for an element without address, e.g. referencing a network object only, and for an unusable address:
        /// unparsable, reversed, or mixing IPv4 and IPv6.
        /// </summary>
        public IPAddressRange? GetIpRange()
        {
            if (IpOperations.TryParseIPRange(IpString, IpEnd, out IPAddressRange? range))
            {
                return range;
            }
            return IpOperations.TryParseIPRange(Cidr?.CidrString, CidrEnd?.CidrString, out range) ? range : null;
        }

        /// <summary>
        /// Returns the address ranges of the elements of one field (e.g. source or destination); elements without
        /// usable address are skipped.
        /// </summary>
        /// <param name="elements">Elements of a request task.</param>
        /// <param name="field">Field whose elements are converted.</param>
        public static List<IPAddressRange> GetIpRanges(IEnumerable<WfReqElement> elements, ElemFieldType field)
        {
            return [.. elements
                .Where(element => element.Field == field.ToString())
                .Select(element => element.GetIpRange())
                .OfType<IPAddressRange>()];
        }
    }
}
