using System;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace JsonParser
{
    // Compatibility adapter: keep the existing tree-view model while using a
    // maintained parser for escapes, Unicode, numbers, and structural validation.
    public static class JSON
    {
        public const int MaxInputLength = 256 * 1024;
        public const int MaxDepth = 64;
        public static JsonElement Parse(string json, int maxLength = MaxInputLength)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            if (maxLength < 1 || json.Length > maxLength) throw new FormatException("JSON input is too large.");
            using (var reader = new JsonTextReader(new StringReader(json))
            {
                Culture = CultureInfo.InvariantCulture,
                DateParseHandling = DateParseHandling.None,
                MaxDepth = MaxDepth
            })
            {
                if (!ReadContent(reader)) throw new FormatException("Expected a JSON value.");
                var result = ReadValue(reader);
                if (ReadContent(reader)) throw new FormatException("Unexpected trailing JSON content.");
                return result;
            }
        }
        public static string Stringify(JsonElement value)
        {
            using (var text = new StringWriter(CultureInfo.InvariantCulture))
            using (var writer = new JsonTextWriter(text))
            {
                WriteValue(writer, value, 0);
                writer.Flush();
                return text.ToString();
            }
        }
        private static bool ReadContent(JsonTextReader reader)
        {
            while (reader.Read()) if (reader.TokenType != JsonToken.Comment) return true;
            return false;
        }
        // Use the streaming parser rather than JToken's dynamic/reflection interfaces.
        // This also retains single quotes, comments and unquoted property names.
        private static JsonElement ReadValue(JsonTextReader reader)
        {
            if (reader.TokenType == JsonToken.StartObject)
            {
                var result = new JsonObject();
                while (ReadContent(reader))
                {
                    if (reader.TokenType == JsonToken.EndObject) return result;
                    if (reader.TokenType != JsonToken.PropertyName) throw new FormatException("Expected a property name.");
                    string name = (string)reader.Value;
                    if (!ReadContent(reader)) break;
                    result.Add(name, ReadValue(reader));
                }
                throw new FormatException("Unclosed JSON object.");
            }
            if (reader.TokenType == JsonToken.StartArray)
            {
                var result = new JsonArray();
                while (ReadContent(reader))
                {
                    if (reader.TokenType == JsonToken.EndArray) return result;
                    result.Push(ReadValue(reader));
                }
                throw new FormatException("Unclosed JSON array.");
            }
            if (reader.TokenType == JsonToken.String || reader.TokenType == JsonToken.Integer || reader.TokenType == JsonToken.Float || reader.TokenType == JsonToken.Boolean || reader.TokenType == JsonToken.Null)
                return new JsonValue(reader.Value);
            throw new FormatException("Unsupported JSON token.");
        }
        private static void WriteValue(JsonTextWriter writer, JsonElement value, int depth)
        {
            if (depth > MaxDepth) throw new FormatException("JSON nesting is too deep.");
            if (value == null) { writer.WriteNull(); return; }
            if (value is JsonObject obj)
            {
                writer.WriteStartObject();
                foreach (var property in obj.KeyValue)
                {
                    writer.WritePropertyName(property.Key);
                    WriteValue(writer, property.Value, depth + 1);
                }
                writer.WriteEndObject();
            }
            else if (value is JsonArray array)
            {
                writer.WriteStartArray();
                foreach (var item in array.Elements) WriteValue(writer, item, depth + 1);
                writer.WriteEndArray();
            }
            else writer.WriteValue(((JsonValue)value).Value);
        }
    }
    public static class StringHelper
    {
        public static string StringToJsonString(string text)
        {
            string quoted = JsonConvert.ToString(text ?? "");
            return quoted.Substring(1, quoted.Length - 2);
        }
        public static string JsonStringToString(string text, char quote = '"')
        {
            // Reading a scalar needs no reflection or dynamically generated code.
            using (var reader = new JsonTextReader(new StringReader(quote + text + quote)))
            {
                reader.DateParseHandling = DateParseHandling.None;
                if (!reader.Read() || reader.TokenType != JsonToken.String) throw new FormatException("Expected a JSON string.");
                string value = (string)reader.Value;
                if (reader.Read()) throw new FormatException("Unexpected trailing JSON content.");
                return value;
            }
        }
        public static string StringToUnicode(string text)
        {
            var result = new StringBuilder();
            foreach (char value in text) result.Append("\\u").Append(((int)value).ToString("x4", CultureInfo.InvariantCulture));
            return result.ToString();
        }
    }
}
