using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Esiur.Schema.Llm
{
    public sealed class LlmEventModel
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("parameters")]
        public List<LlmParameterModel> Parameters { get; set; } = new();

        [JsonPropertyName("annotation")]
        public string? Annotation { get; set; }
    }
}
