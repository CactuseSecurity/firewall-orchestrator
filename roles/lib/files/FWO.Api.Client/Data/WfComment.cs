using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class WfComment : WfCommentBase
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public long Id { get; set; }


        public WfComment()
        { }

        public WfComment(WfComment comment) : base(comment)
        {
            Id = comment.Id;
        }
    }

    public class WfCommentDataHelper
    {
        [JsonProperty("comment"), JsonPropertyName("comment")]
        public WfComment Comment { get; set; } = new ();


        public WfCommentDataHelper()
        {}

        public WfCommentDataHelper(WfComment comment)
        {
            Comment = comment;
        }
    }
}
