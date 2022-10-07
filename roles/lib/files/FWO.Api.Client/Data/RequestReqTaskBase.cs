using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{   
    public enum AutoCreateImplTaskOptions
    {
        never, 
        onlyForOneDevice, 
        forEachDevice, 
        enterInReqTask
    }


    public class RequestReqTaskBase : RequestTaskBase
    {
        [JsonProperty("request_action"), JsonPropertyName("request_action")]
        public string RequestAction { get; set; } = FWO.Api.Data.RequestAction.create.ToString();

        [JsonProperty("reason"), JsonPropertyName("reason")]
        public string? Reason { get; set; }

        [JsonProperty("last_recert_date"), JsonPropertyName("last_recert_date")]
        public DateTime? LastRecertDate { get; set; }

        [JsonProperty("devices"), JsonPropertyName("devices")]
        public string SelectedDevices 
        {  
            get => System.Text.Json.JsonSerializer.Serialize<List<int>>(deviceList) ?? throw new Exception("DeviceList could not be parsed.");
            set
            {
                if(value != null && value != "")
                {
                    deviceList = System.Text.Json.JsonSerializer.Deserialize<List<int>>(value) ?? throw new Exception("value could not be parsed.");
                }
            }
        }

        private List<int> deviceList { get; set; } = new List<int>();


        public RequestReqTaskBase()
        { }

        public RequestReqTaskBase(RequestReqTaskBase reqtask) : base(reqtask)
        {
            RequestAction = reqtask.RequestAction;
            Reason = reqtask.Reason;
            LastRecertDate = reqtask.LastRecertDate;
            SelectedDevices = reqtask.SelectedDevices;
        }

        public List<int> getDeviceList()
        {
            return deviceList;
        }

        public void AddDeviceToList(int deviceId)
        {
            deviceList.Add(deviceId);
        }

        public void ChangeDeviceInList(int index, int deviceId)
        {
            deviceList[index] = deviceId;
        }


        public override bool Sanitize()
        {
            bool shortened = base.Sanitize();
            Reason = Sanitizer.SanitizeOpt(Reason, ref shortened);
            return shortened;
        }
    }
}
