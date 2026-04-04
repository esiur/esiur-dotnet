using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Esiur.Schema.Llm
{
    public sealed class LlmConstantModel
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("value")]
        public object? Value { get; set; }

        [JsonPropertyName("annotation")]
        public string? Annotation { get; set; }
    }
}
