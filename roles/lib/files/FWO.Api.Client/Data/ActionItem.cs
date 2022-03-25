namespace FWO.Api.Data
{
    public enum ActionCode
    {
        DeleteManagement,
        DeleteGateway,
        AddManagement,
        AddGatewayToNewManagement,
        AddGatewayToExistingManagement,
        ReactivateManagement,
        ReactivateGateway,
        WaitForTempLoginFailureToPass
    }

    public class ActionItem
    {
        public int Number { get; set; }

        public long? AlertId { get; set; }

        public long? RefAlertId { get; set; }

        public string? Supermanager { get; set; }

        public string? ActionType { get; set; }

        public int? ManagementId { get; set; }

        public int? DeviceId { get; set; }

        public String? JsonData { get; set; }

        public bool Done { get; set; } = false;

        public ActionItem()
        {}

        public ActionItem(Alert alert)
        {
            Number = 0;
            AlertId = alert.Id;
            Supermanager = alert.Title;
            if (alert.Description == null)
                ActionType = "";
            else
                ActionType = alert.Description;
            ManagementId = alert.ManagementId;
            DeviceId = alert.DeviceId;
            JsonData = alert.JsonData;
            RefAlertId = alert.RefAlert;
        }
    }
}
