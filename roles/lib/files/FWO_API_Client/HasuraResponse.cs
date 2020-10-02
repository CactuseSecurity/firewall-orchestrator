using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace FWO.Api.Client
{
    class HasuraResponse<ResponseType>
    {
        [JsonPropertyName("data")]
        ResponseType[] Data { get; set; }
    }
}
