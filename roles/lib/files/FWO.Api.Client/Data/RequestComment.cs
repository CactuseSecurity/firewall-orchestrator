using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RequestComment : RequestCommentBase
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }


        public RequestComment()
        { }

        public RequestComment(RequestComment comment) : base(comment)
        {
            Id = comment.Id;
        }
    }

    public class RequestCommentDataHelper
    {
        [JsonProperty("comment"), JsonPropertyName("comment")]
        public RequestComment Comment { get; set; } = new ();


        public RequestCommentDataHelper()
        {}

        public RequestCommentDataHelper(RequestComment comment)
        {
            Comment = comment;
        }
    }
}
