using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO_Auth_Client
{
    public class AuthClient
    {
        private readonly HttpClient HttpClient;
        private readonly string AuthServerUri;

        public AuthClient(string AuthServerUri)
        {
            this.AuthServerUri = AuthServerUri;
            HttpClient = new HttpClient();
        }

        public async Task<string> GetJWT(string Username, string Password)
        {
            Dictionary<string, string> Parameters = new Dictionary<string, string>
            {
                { "Username", Username },
                { "Password", Password }
            };

            string ParametersJson = JsonSerializer.Serialize(Parameters);

            StringContent content = new StringContent(ParametersJson);           
            Console.WriteLine("Sending GetJWT Request...");
            HttpResponseMessage response = await HttpClient.PostAsync(AuthServerUri + "jwt", content);
            return await response.Content.ReadAsStringAsync();
        }

        private void ParameterToJson((string, object)[] Parameters)
        {
            StringBuilder JsonString = new StringBuilder();

            JsonString.Append("{ ");

            for (int i = 0; i < Parameters.Length; i++)
            {
                // "ParameterName":
                JsonString.Append("\"" + Parameters[i].Item1 + "\":");

                switch (Parameters[i].Item2)
                {
                    case string Value:
                        // "Value"
                        JsonString.Append("\"" + Value + "\"");
                        break;

                    case int Value:
                        JsonString.Append(Value);
                        break;

                    case bool Value:
                        JsonString.Append(Value);
                        break;

                    case null:
                        JsonString.Append("null");
                        break;
                    
                    default:
                        break;
                }

                JsonString.Append(" ");
            }

            JsonString.Append(" }");
        }
    }
}
