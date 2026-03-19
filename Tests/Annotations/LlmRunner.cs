using Esiur.Resource;
using Esiur.Schema.Llm;
using Esiur.Stores;
using OpenAI;
using OpenAI.Chat;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;

namespace Esiur.Tests.Annotations;

//public sealed class TickState
//{
//    public int Load { get; set; }
//    public int ErrorCount { get; set; }
//    public bool Enabled { get; set; }
//}

//public sealed class LlmDecision
//{
//    public string? Function { get; set; }
//    public string? Reason { get; set; }
//}



public sealed class LlmRunner
{
    private static readonly HashSet<string?> ValidFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        null, "Restart", "ResetErrors", "Enable", "Disable"
    };

    public async Task<(List<TickResult> Results, List<ModelSummary> Summary)> RunAsync(
        
        IReadOnlyList<ModelConfig> models,
        int tickDelayMs = 1000)
    {

        var wh = new Warehouse();

        await wh.Put("store", new MemoryStore());

        var allResults = new List<TickResult>();

        var ticks = new List<TickState>
        {
            new() { Load = 35, ErrorCount = 0, Enabled = true  },
            new() { Load = 88, ErrorCount = 1, Enabled = true  },
            new() { Load = 42, ErrorCount = 4, Enabled = true  },
            new() { Load = 18, ErrorCount = 0, Enabled = false },
            new() { Load = 91, ErrorCount = 5, Enabled = true  },
            new() { Load = 25, ErrorCount = 0, Enabled = true  }
        };

        var expectations = new List<TickExpectation>
        {
            new() { Tick = 1, AllowedFunctions = new HashSet<string?> { null }, Note = "Stable service; no action expected." },
            new() { Tick = 2, AllowedFunctions = new HashSet<string?> { "Restart" }, Note = "Overload; restart expected." },
            new() { Tick = 3, AllowedFunctions = new HashSet<string?> { "Restart", "ResetErrors" }, Note = "High error count; restart or reset is acceptable." },
            new() { Tick = 4, AllowedFunctions = new HashSet<string?> { "Enable" }, Note = "Service disabled; enable expected." },
            new() { Tick = 5, AllowedFunctions = new HashSet<string?> { "Restart" }, Note = "Overload and instability; restart expected." },
            new() { Tick = 6, AllowedFunctions = new HashSet<string?> { null }, Note = "Stable service; no action expected." }
        };

        foreach (var model in models)
        {
            Console.WriteLine($"=== Model: {model.Name} ({model.ModelName}) ===");

            var client = new OpenAIClient(
                model.ApiKey,
                new OpenAIClientOptions { Endpoint = new Uri(model.Endpoint) });

            var chat = client.GetChatClient(model.ModelName);

            Console.WriteLine($"Warming up {model.Name}...");

            await InferAsync(chat,
                "Return {\"function\":null,\"reason\":\"warmup\"}");

            Console.WriteLine("Warmup done");

            // Fresh node instance per model so results are independent.
            var node = await wh.Put("store/service-" + model.Name, new ServiceNode());

            var typeModel = LlmTypeModel.FromTypeDef(node.Instance?.Definition);

            for (int i = 0; i < ticks.Count; i++)
            {
                var tick = ticks[i];
                var expected = expectations[i];

                // Apply tick state before inference
                node.Load = tick.Load;
                node.ErrorCount = tick.ErrorCount;
                node.Enabled = tick.Enabled;

                var loadBefore = node.Load;
                var errorBefore = node.ErrorCount;
                var enabledBefore = node.Enabled;

                var jsonModel = typeModel.ToJson(node);
                var prompt = BuildPrompt(jsonModel, i + 1);

                var sw = Stopwatch.StartNew();
                string raw = await InferAsync(chat, prompt);
                sw.Stop();

                var parsedResult = ParseDecisionWithRepair(raw);

                var firstDecision = parsedResult.First;
                var finalDecision = parsedResult.Final;

                var parsed = finalDecision != null;
                var repaired = parsedResult.Repaired;
                var jsonObjectCount = parsedResult.Count;

                var firstPredicted = NormalizeFunction(firstDecision?.Function);
                var predicted = NormalizeFunction(finalDecision?.Function);

                var allowed = ValidFunctions.Contains(predicted);
                var correct = expected.AllowedFunctions.Contains(predicted);

                var invoked = false;
                if (allowed)
                    invoked = InvokeIfValid(node, predicted);

                var result = new TickResult
                {
                    Model = model.Name,
                    Tick = i + 1,

                    LoadBefore = loadBefore,
                    ErrorCountBefore = errorBefore,
                    EnabledBefore = enabledBefore,

                    RawResponse = raw,
                    FirstPredictedFunction = firstPredicted,
                    PredictedFunction = predicted,
                    Reason = finalDecision?.Reason,

                    Parsed = parsed,
                    Allowed = allowed,
                    Correct = correct,
                    Repaired = repaired,
                    JsonObjectCount = jsonObjectCount,
                    Invoked = invoked,
                    LatencyMs = sw.Elapsed.TotalMilliseconds,

                    LoadAfter = node.Load,
                    ErrorCountAfter = node.ErrorCount,
                    EnabledAfter = node.Enabled,

                    ExpectedText = string.Join(" | ", expected.AllowedFunctions.Select(x => x ?? "null"))
                };

                allResults.Add(result);

                Console.WriteLine($"Tick {result.Tick}");
                Console.WriteLine($"Before: Load={result.LoadBefore}, ErrorCount={result.ErrorCountBefore}, Enabled={result.EnabledBefore}");
                Console.WriteLine($"Expected: {result.ExpectedText}");
                Console.WriteLine($"LLM: {result.RawResponse}");
                Console.WriteLine($"First: {result.FirstPredictedFunction ?? "null"}");
                Console.WriteLine($"Final: {result.PredictedFunction ?? "null"}");
                Console.WriteLine($"Parsed={result.Parsed}, Allowed={result.Allowed}, Correct={result.Correct}, Repaired={result.Repaired}, Invoked={result.Invoked}, Latency={result.LatencyMs:F1} ms");
                Console.WriteLine($"After: Load={result.LoadAfter}, ErrorCount={result.ErrorCountAfter}, Enabled={result.EnabledAfter}");
                Console.WriteLine(new string('-', 72));
                await Task.Delay(tickDelayMs);
            }
        }

        var summary = Summarize(allResults);
        return (allResults, summary);
    }

    private static async Task<string> InferAsync(ChatClient chat, string prompt)
    {
        List<ChatMessage> messages = new()
        {
            new SystemChatMessage(
                "You control a distributed resource. " +
                "Return raw JSON only with fields: function and reason. " +
                "Do not wrap the response in markdown or code fences."),
            new UserChatMessage(prompt)
        };

        var result = await chat.CompleteChatAsync(messages);
        return result.Value.Content[0].Text;
    }

    private static string BuildPrompt(string typeDefJson, int tick)
    {
        return
$@"You are given a runtime type definition for a distributed resource and its current state.
Choose at most one function to call.
Use only the functions defined in the type definition.
Do not invent functions.
Return ONLY valid JSON in this format:
{{ ""function"": ""<<name>>"", ""reason"": ""short explanation"" }}
If the current state is normal and no action is needed, return:
{{ ""function"": null, ""reason"": ""..."" }}.

Input:
{typeDefJson}";
    }

    //private static LlmDecision? ParseDecision(string text)
    //{
    //    try
    //    {
    //        var json = ExtractJson(text);

    //        return JsonSerializer.Deserialize<LlmDecision>(
    //            json,
    //            new JsonSerializerOptions
    //            {
    //                PropertyNameCaseInsensitive = true
    //            });
    //    }
    //    catch
    //    {
    //        return null;
    //    }
    //}

    private static (LlmDecision? First, LlmDecision? Final, bool Repaired, int Count) ParseDecisionWithRepair(string text)
    {
        var objects = ExtractJsonObjects(text);

        if (objects.Count == 0)
            return (null, null, false, 0);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        LlmDecision? first = null;
        LlmDecision? final = null;

        try { first = JsonSerializer.Deserialize<LlmDecision>(objects[0], options); } catch { }
        try { final = JsonSerializer.Deserialize<LlmDecision>(objects[^1], options); } catch { }

        bool repaired = objects.Count > 1 &&
                        NormalizeFunction(first?.Function) != NormalizeFunction(final?.Function);

        return (first, final, repaired, objects.Count);
    }
    private static List<string> ExtractJsonObjects(string text)
    {
        var results = new List<string>();

        if (string.IsNullOrWhiteSpace(text))
            return results;

        text = text.Trim();

        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = text.IndexOf('\n');
            if (firstNewline >= 0)
                text = text[(firstNewline + 1)..];

            var lastFence = text.LastIndexOf("```", StringComparison.Ordinal);
            if (lastFence >= 0)
                text = text[..lastFence];
        }

        int depth = 0;
        int start = -1;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (c == '{')
            {
                if (depth == 0)
                    start = i;

                depth++;
            }
            else if (c == '}')
            {
                if (depth > 0)
                {
                    depth--;

                    if (depth == 0 && start >= 0)
                    {
                        results.Add(text.Substring(start, i - start + 1));
                        start = -1;
                    }
                }
            }
        }

        return results;
    }
    private static string ExtractJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "{}";

        text = text.Trim();

        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = text.IndexOf('\n');
            if (firstNewline >= 0)
                text = text[(firstNewline + 1)..];

            var lastFence = text.LastIndexOf("```", StringComparison.Ordinal);
            if (lastFence >= 0)
                text = text[..lastFence];
        }

        // Fallback: extract first JSON object if extra text exists.
        int start = text.IndexOf('{');
        int end = text.LastIndexOf('}');
        if (start >= 0 && end > start)
            text = text.Substring(start, end - start + 1);

        return text.Trim();
    }

    private static string? NormalizeFunction(string? functionName)
    {
        if (string.IsNullOrWhiteSpace(functionName) ||
            string.Equals(functionName, "null", StringComparison.OrdinalIgnoreCase))
            return null;

        return functionName.Trim();
    }

    private static bool InvokeIfValid(ServiceNode node, string? functionName)
    {
        if (functionName == null)
            return false;

        switch (functionName)
        {
            case "Restart":
                node.Restart();
                return true;

            case "ResetErrors":
                node.ResetErrors();
                return true;

            case "Enable":
                node.Enable();
                return true;

            case "Disable":
                node.Disable();
                return true;

            default:
                return false;
        }
    }

    private static List<ModelSummary> Summarize(List<TickResult> results)
    {
        return results
            .GroupBy(r => r.Model)
            .Select(g =>
            {
                var latencies = g.Select(x => x.LatencyMs).OrderBy(x => x).ToList();

                return new ModelSummary
                {
                    Model = g.Key,
                    TotalTicks = g.Count(),
                    ParseRate = 100.0 * g.Count(x => x.Parsed) / g.Count(),
                    AllowedRate = 100.0 * g.Count(x => x.Allowed) / g.Count(),
                    CorrectRate = 100.0 * g.Count(x => x.Correct) / g.Count(),
                    MeanLatencyMs = g.Average(x => x.LatencyMs),
                    P95LatencyMs = Percentile(latencies, 0.95),
                    RepairRate = 100.0 * g.Count(x => x.Repaired) / g.Count(),
                };
            })
            .OrderBy(x => x.Model)
            .ToList();
    }

    private static double Percentile(List<double> sortedValues, double p)
    {
        if (sortedValues.Count == 0)
            return 0;

        if (sortedValues.Count == 1)
            return sortedValues[0];

        double index = (sortedValues.Count - 1) * p;
        int lower = (int)Math.Floor(index);
        int upper = (int)Math.Ceiling(index);

        if (lower == upper)
            return sortedValues[lower];

        double weight = index - lower;
        return sortedValues[lower] * (1 - weight) + sortedValues[upper] * weight;
    }
}