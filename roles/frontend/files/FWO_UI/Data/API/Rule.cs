using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Ui.Data.API
{
    public class Rule
    {
        StringBuilder result;

        [JsonPropertyName("rule_id")]
        public int Id { get; set; }

        [JsonPropertyName("rule_uid")]
        public string Uid { get; set; }

        [JsonPropertyName("rule_num_numeric")]
        public double OrderNumber { get; set; } 
        
        [JsonPropertyName("rule_name")]
        public string Name { get; set; }

        [JsonPropertyName("rule_comment")]
        public string Comment { get; set; }

        [JsonPropertyName("rule_disabled")]
        public bool Disabled { get; set; }

        [JsonPropertyName("rule_services")]
        public ServiceWrapper[] Services { get; set; }

        [JsonPropertyName("rule_svc_neg")]
        public bool ServiceNegated { get; set; }

        [JsonPropertyName("rule_svc")]
        public string Service { get; set; }

        [JsonPropertyName("rule_src_neg")]
        public bool SourceNegated { get; set; }

        [JsonPropertyName("rule_src")]
        public string Source { get; set; }

        [JsonPropertyName("src_zone")]
        public Zone SourceZone { get; set; }

        [JsonPropertyName("rule_froms")]
        public NetworkLocation[] Froms { get; set; }

        [JsonPropertyName("rule_dst_neg")]
        public bool DestinationNegated { get; set; }

        [JsonPropertyName("rule_dst")]
        public string Destination { get; set; }

        [JsonPropertyName("dst_zone")]
        public Zone DestinationZone { get; set; }

        [JsonPropertyName("rule_tos")]
        public NetworkLocation[] Tos { get; set; }

        [JsonPropertyName("rule_action")]
        public string Action { get; set; }

        [JsonPropertyName("rule_track")]
        public string Track { get; set; }

        [JsonPropertyName("section_header")]
        public string SectionHeader { get; set; }

        public string DisplayNumber(Rule[] rules)
        {
            result = new StringBuilder();
            result.AppendLine($"{Array.IndexOf(rules, this) + 1} <br>");
            result.AppendLine($"DEBUG: {OrderNumber}");
            return result.ToString();
        }

        public string DisplayName()
        {
            return Name;
        }

        public string DisplaySourceZone()
        {
            return SourceZone?.Name;
        }

        public string DisplaySource()
        {
            result = new StringBuilder();
            
            result.AppendLine("<p>");

            if (SourceNegated)
            {
                result.AppendLine("anything but <br>");
            }

            foreach (NetworkLocation source in Froms)
            {
                if (source.User != null)
                {
                    result.AppendLine($"{source.User.Name}@");
                }

                result.Append($"{source.Object.Name}");
                result.Append((source.Object.IP != null ? $" ({source.Object.IP})" : ""));
                result.AppendLine("<br>");
            }

            result.AppendLine("</p>");

            return result.ToString();
        }

        public string DisplayDestinationZone()
        {
            return DestinationZone?.Name;
        }

        public string DisplayDestination()
        {
            result = new StringBuilder();

            result.AppendLine("<p>");

            if (SourceNegated)
            {
                result.AppendLine("anything but <br>");
            }

            foreach (NetworkLocation destination in Tos)
            {
                result.Append($"{destination.Object.Name}");
                result.Append(destination.Object.IP != null ? $" ({destination.Object.IP})" : "");
                result.AppendLine("<br>");
            }

            result.AppendLine("</p>");

            return result.ToString();
        }

        public string DisplayServices()
        {
            result = new StringBuilder();

            result.AppendLine("<p>");

            if (ServiceNegated)
            {
                result.AppendLine("anything but <br>");
            }

            foreach (ServiceWrapper service in Services)
            {
                result.Append($"{service.Content.Name}");
                result.Append(service.Content.DestinationPort != null ? $" ({service.Content.DestinationPort}/{service.Content.Protocol.Name})" : "");
                result.AppendLine("<br>");
            }

            result.AppendLine("</p>");

            return result.ToString();
        }

        public string DisplayAction()
        {
            return Action;
        }

        public string DisplayTrack()
        {
            return Track;
        }

        public string DisplayDisabled()
        {
            return $"<div class=\"oi {(Disabled ? "oi-check" : "oi-x")}\"></div>";
        }

        public string DisplayUid()
        {
            return Uid;
        }
                       
        public string DisplayComment()
        {
            return Comment;
        }
    }
}


