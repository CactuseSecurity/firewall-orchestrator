using RestSharp;
using RestSharp.Serializers.SystemTextJson;
using System.Text.Json;
using FWO.Api.Data;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;
using Newtonsoft.Json;
using FWO.Logging;

namespace FWO.Rest.Client
{
    public class FortiManagerClient
    {
        readonly RestClient restClient;

        public FortiManagerClient(Management fortiManager)
        {
            restClient = new RestClient("https://" + fortiManager.Hostname + ":" + fortiManager.Port + "/jsonrpc");
            restClient.RemoteCertificateValidationCallback += (_, _, _, _) => true;
            restClient.Encoding = Encoding.Latin1;
            restClient.AddDefaultHeader("Content-Type", "application/json");
            JsonSerializerOptions options = new JsonSerializerOptions();
            options.PropertyNameCaseInsensitive = true;
            options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping; // needed to be able to deal with "+" signs in session id
            SystemTextJsonSerializer serializer = new SystemTextJsonSerializer(options);
            restClient.UseSerializer(() => serializer);
        }

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
            request.AddJsonBody(body);
            Log.WriteDebug("Autodiscovery", $"using FortiManager REST API call with body='{body.ToString()}' and paramList='{paramList.ToString()}'");
            return await restClient.ExecuteAsync<FmApiTopLevelHelper>(request);
        }

        public async Task<IRestResponse<FmApiTopLevelHelperDev>> GetDevices(string session)
        {
            string[] fieldArray = { "name", "desc", "hostname", "vdom", "ip", "mgmt_id", "mgt_vdom", "os_type", "os_ver", "platform_str", "dev_status" };
            List<object> paramList = new List<object>();
            paramList.Add(new { fields = fieldArray, url = "/dvmdb/device" });

            var body = new
            {
                @params = paramList,
                method = "get",
                id = 1,
                session = session
            };
            IRestRequest request = new RestRequest("", Method.POST, DataFormat.Json);
            request.AddJsonBody(body);
            return await restClient.ExecuteAsync<FmApiTopLevelHelperDev>(request);
        }

        public async Task<IRestResponse<FmApiTopLevelHelperAssign>> GetPackageAssignmentsPerAdom(string session, string adomName)
        {
            List<object> paramList = new List<object>();
            string urlString = "/pm/config/";

            if (adomName=="global")
                urlString += "global/_package/status";
            else
                urlString += "adom/" + adomName + "/_package/status";
            paramList.Add(new { url = urlString });

            var body = new
            {
                @params = paramList,
                method = "get",
                id = 1,
                session = session
            };
            IRestRequest request = new RestRequest("", Method.POST, DataFormat.Json);
            request.AddJsonBody(body);
            return await restClient.ExecuteAsync<FmApiTopLevelHelperAssign>(request);
        }
    }

    public class SessionAuthInfo
    {
        [JsonProperty("session"), JsonPropertyName("session")]
        public string SessionId { get; set; } = "";
    }

    public class FmApiStatus
    {
        [JsonProperty("code"), JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonProperty("message"), JsonPropertyName("message")]
        public string Message { get; set; } = "";
    }

///////////////////////////////////////////////////////////////////////////////////////////////////
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

        // public List<Package> Packages = new List<Package>();
        public List<Assignment> Assignments = new List<Assignment>();
    }

///////////////////////////////////////////////////////////////////////////////////////////////////

    public class FmApiTopLevelHelperDev
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("status"), JsonPropertyName("status")]
        public FmApiStatus Status { get; set; } = new FmApiStatus();

        [JsonProperty("result"), JsonPropertyName("result")]
        public List<FmApiDataHelperDev> Result { get; set; } = new List<FmApiDataHelperDev>();
    }

    public class FmApiDataHelperDev
    {
        [JsonProperty("data"), JsonPropertyName("data")]
        public List<FortiGate> DeviceList { get; set; } = new List<FortiGate>();
    }
    public class FortiGate
    {
        [JsonProperty("oid"), JsonPropertyName("oid")]
        public int Oid { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("hostname"), JsonPropertyName("hostname")]
        public string Hostname { get; set; } = "";

        // [JsonProperty("ip"), JsonPropertyName("ip")]
        // public string Ip { get; set; } = "";

        [JsonProperty("mgt_vdom"), JsonPropertyName("mgt_vdom")]
        public string MgtVdom { get; set; } = "";
        
        // [JsonProperty("os_ver"), JsonPropertyName("os_ver")]
        // public string OsVer { get; set; } = "";
        
        // [JsonProperty("dev_status"), JsonPropertyName("dev_status")]
        // public string DevStatus { get; set; } = "";
        
        [JsonProperty("vdom"), JsonPropertyName("vdom")]
        public List<Vdom> VdomList { get; set; } = new List<Vdom>();

        // "name", "desc", "hostname", "vdom", "ip", "mgmt_id", "mgt_vdom", "os_type", "os_ver", "platform_str", "dev_status" 
    }
   public class Vdom
    {
        [JsonProperty("oid"), JsonPropertyName("oid")]
        public int Oid { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";
    }

///////////////////////////////////////////////////////////////////////////////////////////////////

    public class FmApiTopLevelHelperAssign
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("status"), JsonPropertyName("status")]
        public FmApiStatus Status { get; set; } = new FmApiStatus();

        [JsonProperty("result"), JsonPropertyName("result")]
        public List<FmApiDataHelperAssign> Result { get; set; } = new List<FmApiDataHelperAssign>();
    }

    public class FmApiDataHelperAssign
    {
        [JsonProperty("data"), JsonPropertyName("data")]
        public List<Assignment> AssignmentList { get; set; } = new List<Assignment>();
    }
    public class Assignment
    {
        [JsonProperty("oid"), JsonPropertyName("oid")]
        public int Oid { get; set; }

        [JsonProperty("dev"), JsonPropertyName("dev")]
        public string DeviceName { get; set; } = "";

        [JsonProperty("vdom"), JsonPropertyName("vdom")]
        public string VdomName { get; set; } = "";

        [JsonProperty("pkg"), JsonPropertyName("pkg")]
        public string PackageName { get; set; } = "";
    }
}
