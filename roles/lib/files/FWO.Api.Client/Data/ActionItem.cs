using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public enum ActionCode
    {
        DeleteManagement,
        DeleteGateway,
        AddManagement,
        AddGatewayToNewManagement,
        AddGatewayToExistingManagement
    }

    public class ActionItem
    {
        public long Id { get; set; }

        public long? RefAlertId { get; set; }

        public string? Supermanager { get; set; }

        public string? ActionType { get; set; }

        public int? ManagementId { get; set; }

        public int? DeviceId { get; set; }

        public String? JsonData { get; set; }

        public ActionItem()
        {}

        public ActionItem(Alert alert)
        {
            Id = alert.Id;
            Supermanager = alert.Title;
            ActionType = alert.Description;
            ManagementId = alert.ManagementId;
            DeviceId = alert.DeviceId;
            JsonData = alert.JsonData;
            RefAlertId = alert.RefAlert;
        }
    }
}
