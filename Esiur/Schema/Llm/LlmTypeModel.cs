using Esiur.Data.Types;
using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Esiur.Schema.Llm
{
    public sealed class LlmTypeModel
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("kind")]
        public string Kind { get; set; } = "";

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("properties")]
        public List<LlmPropertyModel> Properties { get; set; } = new();

        [JsonPropertyName("functions")]
        public List<LlmFunctionModel> Functions { get; set; } = new();

        [JsonPropertyName("events")]
        public List<LlmEventModel> Events { get; set; } = new();

        [JsonPropertyName("constants")]
        public List<LlmConstantModel> Constants { get; set; } = new();

        [JsonPropertyName("usage_rules")]
        public List<string> UsageRules { get; set; } = new();

        public static LlmTypeModel FromJson(string json)
        {
            return JsonSerializer.Deserialize<LlmTypeModel>(json) ?? new LlmTypeModel();
        }

        public string ToJson()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions() { WriteIndented = true });
        }

        public string ToJson(IResource value)
        {
            foreach(var p in Properties)
            {
                if (p.Access == "write")
                    continue;
                var prop = value.GetType().GetProperty(p.Name);
                if (prop != null)
                {
                    var v = prop.GetValue(value);
                    p.Value = v;
                }
            }
            return JsonSerializer.Serialize(this, new JsonSerializerOptions() { WriteIndented = true });

        }

        public static LlmTypeModel FromTypeDef(TypeDef typeDef)
        {
            var m = new LlmTypeModel();

            m.Type = typeDef.Name;
            m.Kind = typeDef.Kind.ToString();

            // summary from annotations
            if (typeDef.Annotations != null && typeDef.Annotations.Count > 0)
            {
                if (typeDef.Annotations.ContainsKey("summary"))
                    m.Summary = typeDef.Annotations["summary"];
                else if (typeDef.Annotations.ContainsKey(""))
                    m.Summary = typeDef.Annotations[""];
            }

            // properties
            foreach (var p in typeDef.Properties)
            {
                var pm = new LlmPropertyModel()
                {
                    Name = p.Name,
                    Type = p.ValueType?.ToString() ?? "unknown",
                    Nullable = p.ValueType?.Nullable ?? false,
                    Access = p.Permission switch
                    {
                        global::Esiur.Resource.PropertyPermission.Read => "read",
                        global::Esiur.Resource.PropertyPermission.Write => "write",
                        global::Esiur.Resource.PropertyPermission.ReadWrite => "readwrite",
                        _ => "readwrite"
                    }
                };

                if (p.Annotations != null && p.Annotations.Count > 0)
                {
                    if (p.Annotations.ContainsKey(""))
                        pm.Annotation = p.Annotations[""];
                    else
                        pm.Annotation = String.Join("; ", p.Annotations.Select(kv => kv.Key + ": " + kv.Value));
                }

                m.Properties.Add(pm);
            }

            // functions
            foreach (var f in typeDef.Functions)
            {
                var fm = new LlmFunctionModel()
                {
                    Name = f.Name,
                    Returns = f.ReturnType?.ToString() ?? "void"
                };

                if (f.Annotations != null && f.Annotations.Count > 0)
                {
                    if (f.Annotations.ContainsKey(""))
                        fm.Annotation = f.Annotations[""];
                    else
                        fm.Annotation = String.Join("; ", f.Annotations.Select(kv => kv.Key + ": " + kv.Value));
                }

                if (f.Arguments != null)
                {
                    foreach (var a in f.Arguments)
                    {
                        var pa = new LlmParameterModel()
                        {
                            Name = a.Name,
                            Type = a.Type?.ToString() ?? "unknown",
                            Nullable = a.Type?.Nullable ?? false
                        };

                        if (a.Annotations != null && a.Annotations.Count > 0)
                        {
                            if (a.Annotations.ContainsKey(""))
                                pa.Annotation = a.Annotations[""];
                            else
                                pa.Annotation = String.Join("; ", a.Annotations.Select(kv => kv.Key + ": " + kv.Value));
                        }

                        fm.Parameters.Add(pa);
                    }
                }

                m.Functions.Add(fm);
            }

            // events
            foreach (var e in typeDef.Events)
            {
                var em = new LlmEventModel()
                {
                    Name = e.Name
                };

                // single argument for event
                if (e.ArgumentType != null)
                {
                    var pa = new LlmParameterModel()
                    {
                        Name = "arg",
                        Type = e.ArgumentType.ToString(),
                        Nullable = e.ArgumentType.Nullable
                    };

                    em.Parameters.Add(pa);
                }

                if (e.Annotations != null && e.Annotations.Count > 0)
                {
                    if (e.Annotations.ContainsKey(""))
                        em.Annotation = e.Annotations[""];
                    else
                        em.Annotation = String.Join("; ", e.Annotations.Select(kv => kv.Key + ": " + kv.Value));
                }

                m.Events.Add(em);
            }

            // constants
            foreach (var c in typeDef.Constants)
            {
                var cm = new LlmConstantModel()
                {
                    Name = c.Name,
                    Type = c.ValueType?.ToString() ?? "unknown",
                    Value = c.Value
                };

                if (c.Annotations != null && c.Annotations.Count > 0)
                {
                    if (c.Annotations.ContainsKey(""))
                        cm.Annotation = c.Annotations[""];
                    else
                        cm.Annotation = String.Join("; ", c.Annotations.Select(kv => kv.Key + ": " + kv.Value));
                }

                m.Constants.Add(cm);
            }

            // usage rules - optional annotation
            if (typeDef.Annotations != null && typeDef.Annotations.Count > 0)
            {
                if (typeDef.Annotations.ContainsKey("usage_rules"))
                {
                    var v = typeDef.Annotations["usage_rules"];
                    if (!String.IsNullOrEmpty(v))
                    {
                        var parts = v.Split(new[] { '\n', ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var p in parts)
                            m.UsageRules.Add(p.Trim());
                    }
                }
            }

            return m;
        }

    }
}
