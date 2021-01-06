using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace FWO.Api.Data // TODO: Change namespace
{
    public class UiUser
    {
        [JsonPropertyName("uiuser_username")]
        public string Name { get; set; }

        [JsonPropertyName("uiuser_id")]
        public int DbId { get; set; }

        [JsonPropertyName("uuid")]
        public string Dn { get; set; }

        public string Password { get; set; }

        [JsonPropertyName("uiuser_email")]
        public string Email { get; set; }

        [JsonPropertyName("uiuser_language")]
        public string Language { get; set; }

        [JsonPropertyName("uiuser_last_login")]
        public DateTime? LastLogin { get; set; }

        [JsonPropertyName("uiuser_last_password_change")]
        public DateTime? LastPasswordChange { get; set; }

        [JsonPropertyName("uiuser_password_must_be_changed")]
        public bool PasswordMustBeChanged { get; set; }

        public string Jwt { get; set; }

        public UiUser()
        {}
        
        public UiUser(UiUser user)
        {
            Name = user.Name;
            DbId = user.DbId;
            Dn = user.Dn;
            Password = user.Password;
            Email = user.Email;
            Language = user.Language;
        }
    }
}
