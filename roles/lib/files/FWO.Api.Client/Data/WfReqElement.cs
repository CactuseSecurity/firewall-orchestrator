﻿using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class WfReqElement : WfElementBase
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonProperty("task_id"), JsonPropertyName("task_id")]
        public long TaskId { get; set; }

        [JsonProperty("request_action"), JsonPropertyName("request_action")]
        public string RequestAction { get; set; } = Data.RequestAction.create.ToString();

        [JsonProperty("device_id"), JsonPropertyName("device_id")]
        public int? DeviceId { get; set; }

        public Cidr Cidr { get; set; } = new Cidr();

        public WfReqElement()
        {}

        public WfReqElement(WfReqElement element) : base (element)
        {
            Id = element.Id;
            TaskId = element.TaskId;
            RequestAction = element.RequestAction;
            DeviceId = element.DeviceId;
            Cidr = new Cidr(element.Cidr != null ? element.Cidr.CidrString : "");
        }
    }
}
