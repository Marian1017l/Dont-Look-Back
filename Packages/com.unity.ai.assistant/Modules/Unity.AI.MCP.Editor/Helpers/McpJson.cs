using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Unity.AI.MCP.Editor.Helpers
{
    static class McpJson
    {
        // Built with JsonSerializer.Create so a host's JsonConvert.DefaultSettings can never reach the MCP wire format.
        static readonly JsonSerializer s_Serializer = JsonSerializer.Create(new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver(),
            CheckAdditionalContent = true
        });

        public static string Serialize(object value, Formatting formatting = Formatting.None)
        {
            var stringWriter = new StringWriter(CultureInfo.InvariantCulture);
            using (var jsonWriter = new JsonTextWriter(stringWriter) { Formatting = formatting })
            {
                s_Serializer.Serialize(jsonWriter, value);
            }
            return stringWriter.ToString();
        }

        public static T Deserialize<T>(string json)
        {
            using var jsonReader = new JsonTextReader(new StringReader(json));
            return s_Serializer.Deserialize<T>(jsonReader);
        }

        public static JArray ToJArray(object value) => JArray.FromObject(value, s_Serializer);

        public static JObject ToJObject(object value) => JObject.FromObject(value, s_Serializer);
    }
}
