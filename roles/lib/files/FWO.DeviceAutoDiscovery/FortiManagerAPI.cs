using FWO.Api.Client;
using FWO.Data;
using FWO.Logging;
using RestSharp;

namespace FWO.DeviceAutoDiscovery
{
    public class FortiManagerClient : RestApiClient
    {
        public FortiManagerClient(Management fortiManager) : base("https://" + fortiManager.Hostname + ":" + fortiManager.Port + "/jsonrpc")
        { }

        public async Task<RestResponse<SessionAuthInfo>> AuthenticateUser(string? user, string pwd, string domainString = "")
        {
            List<object> dataList = [];
            dataList.Add(new { passwd = pwd, user = user });

            List<object> paramList = [];
            paramList.Add(new { data = dataList, url = "/sys/login/user" });

            var body = new
            {
                method = "exec",
                id = 1,
                @params = paramList // because "params" is a c# keyword, we have to escape it here with @
            };
            RestRequest request = new("", Method.Post);
            request.AddJsonBody(body);
            return await restClient.ExecuteAsync<SessionAuthInfo>(request);
        }

        public async Task<RestResponse<SessionAuthInfo>> DeAuthenticateUser(string session)
        {
            List<object> paramList = [];
            paramList.Add(new { session = session, url = "/sys/logout" });

            var body = new
            {
                method = "exec",
                id = 1,
                @params = paramList // because "params" is a c# keyword, we have to escape it here with @
            };
            RestRequest request = new("", Method.Post);
            request.AddJsonBody(body);
            return await restClient.ExecuteAsync<SessionAuthInfo>(request);
        }

        public async Task<string> GetFortiManagerDetails(string sessionId)
        {
            string[] fieldArray = ["name", "oid", "uuid"];
            List<object> paramList = [new { fields = fieldArray, url = "/sys/status" }];

            var body = new
            {
                @params = paramList,
                method = "get",
                id = 1,
                session = sessionId
            };
            RestRequest request = new("", Method.Post);
            request.AddJsonBody(body);
            Log.WriteDebug("Autodiscovery", $"using FortiManager REST API call with body='{body.ToString()}' and paramList='{paramList.ToString()}'");
            await restClient.ExecuteAsync<FmApiTopLevelHelper>(request);

            return "dummy-uid";
        }

        public async Task<RestResponse<FmApiTopLevelHelper>> GetAdoms(string sessionId)
        {
            string[] fieldArray = { "name", "oid", "uuid" };
            List<object> paramList = [];
            paramList.Add(new { fields = fieldArray, url = "/dvmdb/adom" });

            var body = new
            {
                @params = paramList,
                method = "get",
                id = 1,
                session = sessionId
            };
            RestRequest request = new("", Method.Post);
            request.AddJsonBody(body);
            Log.WriteDebug("Autodiscovery", $"using FortiManager REST API call with body='{body.ToString()}' and paramList='{paramList.ToString()}'");
            return await restClient.ExecuteAsync<FmApiTopLevelHelper>(request);
        }

        public async Task<RestResponse<FmApiTopLevelHelperDev>> GetDevices(string sessionId)
        {
            string[] fieldArray = ["name", "desc", "hostname", "vdom", "ip", "mgmt_id", "mgt_vdom", "os_type", "os_ver", "platform_str", "dev_status"];
            List<object> paramList = [];
            paramList.Add(new { fields = fieldArray, url = "/dvmdb/device" });

            var body = new
            {
                @params = paramList,
                method = "get",
                id = 1,
                session = sessionId
            };
            RestRequest request = new("", Method.Post);
            request.AddJsonBody(body);
            return await restClient.ExecuteAsync<FmApiTopLevelHelperDev>(request);
        }

        public async Task<RestResponse<FmApiTopLevelHelperDev>> GetDevicesPerAdom(string sessionId, string adomName)
        {
            string[] fieldArray = ["name", "desc", "hostname", "vdom", "ip", "mgmt_id", "mgt_vdom", "os_type", "os_ver", "platform_str", "dev_status"];
            List<object> paramList = [];
            paramList.Add(new { fields = fieldArray, url = $"/dvmdb/adom/{adomName}/device" });

            var body = new
            {
                @params = paramList,
                method = "get",
                id = 1,
                session = sessionId
            };
            RestRequest request = new("", Method.Post);
            request.AddJsonBody(body);
            return await restClient.ExecuteAsync<FmApiTopLevelHelperDev>(request);
        }
    }
}
