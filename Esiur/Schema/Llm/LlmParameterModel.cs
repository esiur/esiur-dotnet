using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Esiur.Schema.Llm
{
    public sealed class LlmParameterModel
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("annotation")]
        public string? Annotation { get; set; }

        [JsonPropertyName("nullable")]
        public bool Nullable { get; set; }
    }
}
