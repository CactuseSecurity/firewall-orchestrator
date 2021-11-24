using System.Text.Json.Serialization;

namespace FWO.ApiClient
{
    public class ReturnId
    {
        [JsonPropertyName("newId")]
        public int NewId { get; set; }

        [JsonPropertyName("UpdatedId")]
        public int UpdatedId { get; set; }

        [JsonPropertyName("DeletedId")]
        public int DeletedId { get; set; }

        [JsonPropertyName("affected_rows")]
        public int AffectedRows { get; set; }

        [JsonPropertyName("uiuser_password_must_be_changed")]
        public bool PasswordMustBeChanged { get; set; }
    }
    
    public class NewReturning
    {
        [JsonPropertyName("returning")]
        public ReturnId[]? ReturnIds { get; set; }
    }
}
