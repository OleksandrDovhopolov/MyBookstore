using System;
using Newtonsoft.Json.Linq;

namespace Save.Storage
{
    // Нормализует HTTP-ответ сервера в строку, пригодную для десериализации SaveData.
    // Поддерживает три формата ответа:
    //   data-string — { "data": "<json-string>" }
    //   data-json   — { "data": { ... } }
    //   raw-json    — прямой JSON без обёртки
    public static class SaveGlobalPayloadParser
    {
        public static JToken ExtractPayloadStrict(JToken response)
        {
            if (response == null || response.Type is JTokenType.Null or JTokenType.Undefined)
                throw new InvalidOperationException("Save/global response is empty.");

            if (response.Type == JTokenType.String)
            {
                var rawRoot = response.Value<string>();
                if (string.IsNullOrWhiteSpace(rawRoot))
                    throw new InvalidOperationException("Save/global response root string is empty.");
                try
                {
                    return JToken.Parse(rawRoot);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Save/global root string is not valid JSON. {ex.Message}", ex);
                }
            }

            if (response is not JObject root)
                return response;

            if (!root.TryGetValue("data", StringComparison.OrdinalIgnoreCase, out var dataToken))
                return response;

            if (dataToken == null || dataToken.Type is JTokenType.Null or JTokenType.Undefined)
                throw new InvalidOperationException("Save/global envelope contains empty data field.");

            if (dataToken.Type == JTokenType.String)
            {
                var rawData = dataToken.Value<string>();
                if (string.IsNullOrWhiteSpace(rawData))
                    throw new InvalidOperationException("Save/global envelope contains blank data string.");
                try
                {
                    return JToken.Parse(rawData);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Save/global data string is not valid JSON. {ex.Message}", ex);
                }
            }

            return dataToken;
        }

        public static string ExtractDataForStorage(string responseText, out string extractionMode)
        {
            if (string.IsNullOrWhiteSpace(responseText))
            {
                extractionMode = "empty";
                return responseText;
            }

            try
            {
                var token = JToken.Parse(responseText);
                if (token is JObject root && root.TryGetValue("data", out var dataToken))
                {
                    if (dataToken?.Type == JTokenType.String)
                    {
                        extractionMode = "data-string";
                        return dataToken.Value<string>();
                    }

                    if (dataToken is { Type: JTokenType.Object or JTokenType.Array })
                    {
                        extractionMode = "data-json";
                        return dataToken.ToString();
                    }
                }

                extractionMode = "raw-json";
                return responseText;
            }
            catch
            {
                extractionMode = "raw-text";
                return responseText;
            }
        }
    }
}
