using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Ui.Data.API
{
    public class NetworkService
    {
        [JsonPropertyName("svc_id")]
        public int Id { get; set; }

        [JsonPropertyName("svc_name")]
        public string Name { get; set; }

        [JsonPropertyName("svc_uid")]
        public string Uid { get; set; }

        [JsonPropertyName("svc_port")]
        public int? DestinationPort { get; set; }

        [JsonPropertyName("svc_port_end")]
        public int? DestinationPortEnd { get; set; }

        [JsonPropertyName("svc_source_port")]
        public int? SourcePort { get; set; }

        [JsonPropertyName("svc_source_port_end")]
        public int? SourcePortEnd { get; set; }

        [JsonPropertyName("svc_code")]
        public string Code { get; set; }

        [JsonPropertyName("svc_timeout")]
        public int? Timeout { get; set; }

        [JsonPropertyName("svc_typ_id")]
        public int? TypeId { get; set; }

        [JsonPropertyName("active")]
        public bool Active { get; set; }

        [JsonPropertyName("svc_create")]
        public int Create { get; set; }

        [JsonPropertyName("svc_last_seen")]
        public int LastSeen { get; set; }

        [JsonPropertyName("service_type")]
        public NetworkServiceType Type { get; set; }

        [JsonPropertyName("svc_comment")]
        public string Comment { get; set; }

        [JsonPropertyName("svc_color_id")]
        public int? ColorId { get; set; }

        [JsonPropertyName("id_proto_id")]
        public int? ProtoId { get; set; }

        [JsonPropertyName("protocol_name")]
        public NetworkProtocol Protocol { get; set; }

        [JsonPropertyName("svc_member_names")]
        public string MemberNames { get; set; }

        [JsonPropertyName("svc_member_refs")]
        public string MemberRefs { get; set; }

        [JsonPropertyName("svcgrps")]
        public Group<NetworkService>[] ServiceGroups { get; set; }

        [JsonPropertyName("svcgrp_flats")]
        public GroupFlat<NetworkService>[] ServiceGroupFlats { get; set; }

        //  svc_id
        //  svc_name
        //  svc_uid
        //  svc_port
        //  svc_port_end
        //  svc_source_port
        //  svc_source_port_end
        //  svc_code
        //  svc_timeout
        //  svc_typ_id
        //  active
        //  svc_create
        //  svc_last_seen
        //  service_type: stm_svc_typ {
        //    name: svc_typ_name
        //  }
        //  svc_comment
        //  svc_color_id
        //  ip_proto_id
        //  protocol_name: stm_ip_proto {
        //    name: ip_proto_name
        //  }
        //  svc_member_names
        //  svc_member_refs
        //  svcgrps {
        //    id: svcgrp_member_id
        //    byId: serviceBySvcgrpMemberId {
        //      svc_id
        //      svc_name
        //    }
        //  }
        //  svcgrp_flats {
        //    flat_id: svcgrp_flat_id
        //    byFlatId: serviceBySvcgrpFlatMemberId {
        //      svc_id
        //      svc_name
        //    }
        //  }
    }
}
