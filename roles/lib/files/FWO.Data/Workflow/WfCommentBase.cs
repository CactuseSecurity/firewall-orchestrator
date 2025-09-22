using System.Text.Json.Serialization;
using FWO.Basics;
using Newtonsoft.Json;

namespace FWO.Data.Workflow
{
    public class WfCommentBase
    {
        [JsonProperty("ref_id"), JsonPropertyName("ref_id")]
        public long? RefId { get; set; }

        [JsonProperty("scope"), JsonPropertyName("scope")]
        public string Scope { get; set; } = "";

        [JsonProperty("creation_date"), JsonPropertyName("creation_date")]
        public DateTime CreationDate { get; set; } = DateTime.Now;

        [JsonProperty("creator"), JsonPropertyName("creator")]
        public UiUser Creator { get; set; } = new UiUser();

        [JsonProperty("comment_text"), JsonPropertyName("comment_text")]
        public string CommentText { get; set; } = "";


        public WfCommentBase()
        { }

        public WfCommentBase(WfCommentBase comment)
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
            Scope = Scope.SanitizeMand(ref shortened);
            CommentText = CommentText.SanitizeMand(ref shortened);
            return shortened;
        }
    }
}
