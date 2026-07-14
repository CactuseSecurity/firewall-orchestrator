using FWO.Basics;
using Newtonsoft.Json;
using System.Net;
using System.Text.Json.Serialization;

namespace FWO.Data.Workflow
{
    public enum WfObjectScopes
    {
        None = 0,
        Ticket = 1,
        RequestTask = 2,
        ImplementationTask = 3,
        Approval = 4
    }

    public class WfStatefulObject
    {
        [JsonProperty("state_id"), JsonPropertyName("state_id")]
        public int StateId
        {
            get { return stateId; }
            set
            {
                if (!stateChanged)
                {
                    stateChanged = oldStateId != value;
                    oldStateId = stateId;
                }
                stateId = value;
            }
        }

        [JsonProperty("current_handler"), JsonPropertyName("current_handler")]
        public UiUser? CurrentHandler { get; set; }

        [JsonProperty("recent_handler"), JsonPropertyName("recent_handler")]
        public UiUser? RecentHandler { get; set; }

        [JsonProperty("assigned_group"), JsonPropertyName("assigned_group")]
        public string? AssignedGroup { get; set; }


        // need private declarations, else we get problems with request_reqtask_arr_rel_insert_input in newTicket
        private int stateId;
        private int oldStateId;
        private bool stateChanged = false;
        private bool stateChangedByCreation = false;
        private string? optComment;

        public string? OptComment()
        {
            return optComment;
        }

        public void SetOptComment(string? comm)
        {
            optComment = comm;
        }

        public bool StateChanged()
        {
            return stateChanged;
        }

        public int ChangedFrom()
        {
            return oldStateId;
        }

        public bool StateChangedByCreation()
        {
            return stateChangedByCreation;
        }

        public void MarkCreatedStateChanged(int newStateId)
        {
            StateId = 0;
            ResetStateChanged();
            StateId = newStateId;
            stateChangedByCreation = true;
        }

        public void ResetStateChanged()
        {
            oldStateId = stateId;
            stateChanged = false;
            stateChangedByCreation = false;
        }

        public WfStatefulObject()
        { }

        public WfStatefulObject(WfStatefulObject obj)
        {
            stateId = obj.stateId;
            oldStateId = obj.oldStateId;
            stateChanged = obj.stateChanged;
            stateChangedByCreation = obj.stateChangedByCreation;
            optComment = obj.optComment;
            CurrentHandler = obj.CurrentHandler;
            RecentHandler = obj.RecentHandler;
            AssignedGroup = obj.AssignedGroup;
        }

        public virtual bool Sanitize()
        {
            bool shortened = false;
            optComment = optComment.SanitizeOpt(ref shortened);
            AssignedGroup = AssignedGroup.SanitizeLdapPathOpt(ref shortened);
            return shortened;
        }

        public static string DisplayAllComments(List<WfCommentDataHelper> Comments, bool asMarkup = false)
        {
            string allComments = "";
            foreach (var comment in Comments)
            {
                string creatorName = asMarkup ? WebUtility.HtmlEncode(comment.Comment.Creator.Name) : comment.Comment.Creator.Name;
                string commentText = asMarkup ? WebUtility.HtmlEncode(comment.Comment.CommentText) : comment.Comment.CommentText;
                allComments += comment.Comment.CreationDate.ToShortDateString() + " "
                            + creatorName + ": "
                            + commentText + (asMarkup ? "<br>" : "\r\n");
            }
            return allComments;
        }
    }
}
