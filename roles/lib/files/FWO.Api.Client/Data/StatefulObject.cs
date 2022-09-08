using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class StatefulObject
    {
        [JsonProperty("state_id"), JsonPropertyName("state_id")]
        public int StateId 
        { 
            get { return stateId; } 
            set 
            {
                if(!stateChanged)
                {
                    stateChanged = oldStateId != value;
                    oldStateId = stateId;
                }
                stateId = value; 
            } 
        }

         // need private declarations, else we get problems with request_task_arr_rel_insert_input in newTicket
        private int stateId;
        private int oldStateId;
        private bool stateChanged = false;
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

        public void ResetStateChanged()
        {
            oldStateId = stateId;
            stateChanged = false;
        }

        public StatefulObject()
        { }

        public StatefulObject(StatefulObject obj)
        {
            stateId = obj.stateId;
            oldStateId = obj.oldStateId;
            stateChanged = obj.stateChanged;
            optComment = obj.optComment;
        }

        public virtual bool Sanitize()
        {
            bool shortened = false;
            optComment = Sanitizer.SanitizeOpt(optComment, ref shortened);
            return shortened;
        }
    }
}
