using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RequestCommentBase
    {
        [JsonProperty("ref_id"), JsonPropertyName("ref_id")]
        public long RefId { get; set; }

        [JsonProperty("scope"), JsonPropertyName("scope")]
        public string Scope { get; set; } = "";

        [JsonProperty("creation_date"), JsonPropertyName("creation_date")]
        public DateTime CreationDate { get; set; } = DateTime.Now;

        [JsonProperty("creator_id"), JsonPropertyName("creator_id")]
        public UiUser Creator { get; set; } = new UiUser();

        [JsonProperty("comment_text"), JsonPropertyName("comment_text")]
        public string CommentText { get; set; } = "";


        public RequestCommentBase()
        { }

        public RequestCommentBase(RequestCommentBase comment)
        {
            RefId = comment.RefId;
            Scope = comment.Scope;
            CreationDate = comment.CreationDate;
            Creator = comment.Creator;
            CommentText = comment.CommentText;
        }

        public virtual bool Sanitize()
        {
            bool shortened = false;
            Scope = Sanitizer.SanitizeMand(Scope, ref shortened);
            CommentText = Sanitizer.SanitizeMand(CommentText, ref shortened);
            return shortened;
        }
    }
}
