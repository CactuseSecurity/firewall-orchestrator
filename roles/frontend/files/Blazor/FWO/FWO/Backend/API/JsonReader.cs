using FWO.Backend.Data.API;
using Microsoft.AspNetCore.Authentication;
using System.Collections.Generic;
using System.Data;
using System.Text.Json;
using static System.Text.Json.JsonElement;

namespace FWO
{
    public static class JsonReader
    {
        public static Management[] JsonToReport(string Json)
        {
            string testJson = JsonSerializer.Serialize(new Management[] { new Management { Id = 2 }, new Management { Id = 3 } });

            JsonDocument JsonRoot = JsonDocument.Parse(Json);
            ObjectEnumerator test = JsonRoot.RootElement.EnumerateObject();
            test.MoveNext();
            string hallo = test.Current.Value.GetString("management");
            return JsonSerializer.Deserialize<Management[]>(hallo);
        }

        /*
        public static void ReadFilter(string JsonStringFilter)
        {
            JsonElement DataRoot = ReadString(JsonStringFilter);
            

            JsonElement.ObjectEnumerator DataRootEnum = DataRoot.EnumerateObject();

            if (DataRootEnum.MoveNext())
            {
                JsonProperty JsonFilter = DataRootEnum.Current;
                JsonElement JsonFilterData = JsonFilter.Value;

                JsonElement.ArrayEnumerator JsonFilterEnum = JsonFilterData.EnumerateArray();

                if (JsonFilterEnum.MoveNext())
                {
                    List<AttributeDefinition> AttributeDefinitions = new List<AttributeDefinition>();
                    List<Attribute> Attributes = new List<Attribute>();

                    JsonElement.ObjectEnumerator JsonFilterDataObjectEnum = JsonFilterEnum.Current.EnumerateObject();


                    while (JsonFilterDataObjectEnum.MoveNext())
                    {
                        AttributeDefinitions.Add(new AttributeDefinition
                        {
                            Name = JsonFilterDataObjectEnum.Current.Name,
                            Type = JsonFilterDataObjectEnum.Current.Value.ValueKind
                        });

                        switch (JsonFilterDataObjectEnum.Current.Value.ValueKind)
                        {
                            case JsonValueKind.Undefined:
                                
                                break;
                            case JsonValueKind.Object:

                                break;
                            case JsonValueKind.Array:

                                break;
                            case JsonValueKind.String:
                                JsonFilterDataObjectEnum.Current.Value.GetString();
                                break;
                            case JsonValueKind.Number:
                                JsonFilterDataObjectEnum.Current.Value.GetDouble();
                                break;
                            case JsonValueKind.True:
                                JsonFilterDataObjectEnum.Current.Value.GetBoolean();
                                break;
                            case JsonValueKind.False:
                                JsonFilterDataObjectEnum.Current.Value.GetBoolean();
                                break;
                            case JsonValueKind.Null:
                                
                                break;
                            default:

                                break;
                        }

                        Attributes.Add(new Attribute
                        {
                            JsonFilterDataObjectEnum.Current.Value.
                        });
                    }
                }

                else
                {

                }

                while (JsonFilterEnum.MoveNext())
                {
                    JsonFilterEnum.Current;
                }
            }

            FilterData FilterData = new FilterData
            {
                Name = JsonFilter.Name,
                AttributeDefinitions = AttributeDefinitions,

            };
        }

        private static JsonElement ReadString(string JsonString)
        {
            JsonDocument jsonDocument = JsonDocument.Parse(JsonString);
            JsonElement DataRoot = jsonDocument.RootElement.GetProperty("data");


            return DataRoot;
        }

        private static void GetFilterRec(JsonElement CurrentElement)
        {
            switch (CurrentElement.ValueKind)
            {
                case JsonValueKind.Undefined:
                    break;
                case JsonValueKind.Object:
                    CurrentElement.EnumerateObject();
                    break;
                case JsonValueKind.Array:
                    CurrentElement.EnumerateArray();
                    break;
                case JsonValueKind.String:
                    break;
                case JsonValueKind.Number:
                    CurrentElement.GetDouble();
                    break;
                case JsonValueKind.True:
                //return true;
                case JsonValueKind.False:
                //return false;
                case JsonValueKind.Null:
                    //return null;
                    break;
                default:
                    break;
            }
        }
        */
    }
}
