using RestSharp;
using RestSharp.Authenticators;
using RestSharp.Serializers.SystemTextJson;
using System.Text.Json;
using FWO.Api.Data;
using System.Text;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Rest.Client
{
    public class FortiManagerClient
    {
        readonly RestClient restClient;

        public FortiManagerClient(Management fortiManager)
        {
            restClient = new RestClient("https://" + fortiManager.Hostname + ":" + fortiManager.Port + "/jsonrpc");
            restClient.RemoteCertificateValidationCallback += (_, _, _, _) => true;

            JsonSerializerOptions options = new JsonSerializerOptions();
            options.PropertyNameCaseInsensitive = true;
            SystemTextJsonSerializer serializer = new SystemTextJsonSerializer(options);
            restClient.UseSerializer(() => serializer);
        }

        // public async Task<IRestResponse<string>> AuthenticateUser(string? user, string pwd)
        // {
        //     List<object> dataList = new List<object>();
        //     dataList.Add(new { passwd = pwd, user = user });

        //     List<object> paramList = new List<object>();
        //     paramList.Add(new { data = dataList, url = "/sys/login/user" });

        //     var body = new
        //     {
        //         method = "exec",
        //         id = 1,
        //         @params = paramList // because "params" is a c# keyword, we have to escape it here with @
        //     };
        //     IRestRequest request = new RestRequest("/jsonrpc", Method.POST, DataFormat.Json);
        //     request.AddJsonBody(body);
        //     return await restClient.ExecuteAsync<string>(request);
        // }

        public async Task<IRestResponse<SessionAuthInfo>> AuthenticateUser(string? user, string pwd)
        {
            List<object> dataList = new List<object>();
            dataList.Add(new { passwd = pwd, user = user });

            List<object> paramList = new List<object>();
            paramList.Add(new { data = dataList, url = "/sys/login/user" });

            var body = new
            {
                method = "exec",
                id = 1,
                @params = paramList // because "params" is a c# keyword, we have to escape it here with @
            };
            IRestRequest request = new RestRequest("", Method.POST, DataFormat.Json);
            request.AddJsonBody(body);
            // request.OnBeforeDeserialization = response =>
            // {
            //     Encoding encoding = Encoding.GetEncoding("ISO-8859-1");
            //     response.Content = encoding.GetString(response.RawBytes);
            // };
            // request.OnBeforeRequest = response =>
            // {
            //     response.Encoding = Encoding.GetEncoding("ISO-8859-1");
            //     // response.Encoding = Encoding.ASCII;
            // };
            return await restClient.ExecuteAsync<SessionAuthInfo>(request);
        }

        public async Task<IRestResponse<SessionAuthInfo>> DeAuthenticateUser(string session)
        {
            List<object> paramList = new List<object>();
            paramList.Add(new { session = session, url = "/sys/logout" });

            var body = new
            {
                method = "exec",
                id = 1,
                @params = paramList // because "params" is a c# keyword, we have to escape it here with @
            };
            IRestRequest request = new RestRequest("", Method.POST, DataFormat.Json);
            request.AddJsonBody(body);
            // request.OnBeforeDeserialization = response =>
            // {
            //     Encoding encoding = Encoding.GetEncoding("ISO-8859-1");
            //     response.Content = encoding.GetString(response.RawBytes);
            // };
            // request.OnBeforeRequest = response =>
            // {
            //     response.Encoding = Encoding.GetEncoding("ISO-8859-1");
            //     // response.Encoding = Encoding.ASCII;
            // };
            return await restClient.ExecuteAsync<SessionAuthInfo>(request);
        }

        public async Task<IRestResponse<FmApiTopLevelHelper>> GetAdoms(string session)
        {
            string[] fieldArray = { "name", "oid", "uuid" };
            List<object> paramList = new List<object>();
            paramList.Add(new { fields = fieldArray, url = "/dvmdb/adom" });

            var body = new
            {
                @params = paramList,
                method = "get",
                id = 1,
                session = session
            };
            IRestRequest request = new RestRequest("", Method.POST, DataFormat.Json);
            request.OnBeforeRequest = request =>
            {
                // request.Encoding = Encoding.GetEncoding("ISO-8859-1");
                request.Encoding = Encoding.ASCII;
            };
            request.AddJsonBody(body);
            // request.OnBeforeDeserialization = response =>
            // {
            //     Encoding encoding = Encoding.GetEncoding("ISO-8859-1");
            //     response.Content = encoding.GetString(response.RawBytes);
            // };
            return await restClient.ExecuteAsync<FmApiTopLevelHelper>(request);
        }

        public async Task<IRestResponse<FmApiTopLevelHelper>> GetDevices(string session)
        {
            string[] fieldArray = { "name", "oid", "uuid" };
            List<object> paramList = new List<object>();
            paramList.Add(new { fields = fieldArray, url = "/dvmdb/adom" });

            var body = new
            {
                @params = paramList,
                method = "get",
                id = 1,
                session = session
            };
            IRestRequest request = new RestRequest("/jsonrpc", Method.POST, DataFormat.Json);
            request.AddJsonBody(body);
            // request.OnBeforeDeserialization = response =>
            // {
            //     Encoding encoding = Encoding.GetEncoding("ISO-8859-1");
            //     response.Content = encoding.GetString(response.RawBytes);
            // };
            // request.OnBeforeRequest = response =>
            // {
            //     response.Encoding = Encoding.GetEncoding("ISO-8859-1");
            //     // response.Encoding = Encoding.ASCII;
            // };
            return await restClient.ExecuteAsync<FmApiTopLevelHelper>(request);
        }
    }

    public class SessionAuthInfo
    {
        [JsonProperty("session"), JsonPropertyName("session")]
        public string SessionId { get; set; } = "";
    }

    public class FmApiTopLevelHelper
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("status"), JsonPropertyName("status")]
        public FmApiStatus Status { get; set; } = new FmApiStatus();

        [JsonProperty("result"), JsonPropertyName("result")]
        public List<FmApiDataHelper> Result { get; set; } = new List<FmApiDataHelper>();
    }

    public class FmApiDataHelper
    {
        [JsonProperty("data"), JsonPropertyName("data")]
        public List<Adom> AdomList { get; set; } = new List<Adom>();
    }

    public class Adom
    {
        [JsonProperty("oid"), JsonPropertyName("oid")]
        public int Oid { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("uuid"), JsonPropertyName("uuid")]
        public string Uid { get; set; } = "";
    }
    public class FmApiStatus
    {
        [JsonProperty("code"), JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonProperty("message"), JsonPropertyName("message")]
        public string Message { get; set; } = "";
    }

    public class FortiGate
    {
        [JsonProperty("oid"), JsonPropertyName("oid")]
        public int Oid { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

    }
}
