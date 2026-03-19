using Esiur.Schema.Llm;
using OpenAI;
using OpenAI.Chat;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;

namespace Esiur.Tests.Annotations
{
    public class LlmRunner
    {
        public async Task RunAsync(ServiceNode node, string endpoint, ApiKeyCredential apiKey, string modelName,
            int tickDelayMs = 1000)
        {
            var client = new OpenAIClient(apiKey, new OpenAIClientOptions() { Endpoint = new Uri(endpoint) });
            var chat = client.GetChatClient("microsoft/phi-4");

            var typeModel = LlmTypeModel.FromTypeDef(node.Instance?.Definition);

            var ticks = new List<TickState>
            {
                new() { Load = 35, ErrorCount = 0, Enabled = true  },
                new() { Load = 88, ErrorCount = 1, Enabled = true  },
                new() { Load = 42, ErrorCount = 4, Enabled = true  },
                new() { Load = 18, ErrorCount = 0, Enabled = false },
                new() { Load = 91, ErrorCount = 5, Enabled = true  },
                new() { Load = 25, ErrorCount = 0, Enabled = true  }
            };

            for (int i = 0; i < ticks.Count; i++)
            {
                var tick = ticks[i];

                // Simulate property changes for this tick
                node.Load = tick.Load;
                node.ErrorCount = tick.ErrorCount;
                node.Enabled = tick.Enabled;

                var jsonModel = typeModel.ToJson(node);
                Console.WriteLine($"Tick {i + 1}");
                Console.WriteLine($"State: Load={node.Load}, ErrorCount={node.ErrorCount}, Enabled={node.Enabled}");

                var prompt = BuildPrompt(jsonModel, node, i + 1);

                string llmRaw = await InferAsync(chat, prompt);
                var decision = ParseDecision(llmRaw);

                bool invoked = InvokeIfValid(node, decision?.Function);

                Console.WriteLine($"LLM: {llmRaw}");
                Console.WriteLine($"Invoked: {invoked}");
                Console.WriteLine($"After: Load={node.Load}, ErrorCount={node.ErrorCount}, Enabled={node.Enabled}");
                Console.WriteLine(new string('-', 60));

                await Task.Delay(tickDelayMs);
            }

        }

        async Task<string> InferAsync(
    ChatClient chat,
    string prompt)
        {

            List<ChatMessage> messages = new List<ChatMessage>
            {
                new SystemChatMessage("You control a distributed resource. " +
                "Return only JSON with fields: function and reason."),
                new UserChatMessage(prompt)
            };

            var result = await chat.CompleteChatAsync(messages);

            return result.Value.Content[0].Text;
        }
        private static string BuildPrompt(string typeDefJson, ServiceNode node, int tick)
        {
            return
$@"You are given a runtime type definition for a distributed resource and its current state.
Choose at most one function to call.
Use only the functions defined in the type definition.
Do not invent functions.
If no action is needed, return function as null.
Return only JSON in this format:
{{ ""function"": ""Restart|ResetErrors|Enable|Disable|null"", ""reason"": ""short explanation"" }}

Type Definition:
{typeDefJson}";

//Current Tick: {tick}
//Current State:
//{{
//  ""Load"": {node.Load},
//  ""ErrorCount"": {node.ErrorCount},
//  ""Enabled"": {(node.Enabled ? "true" : "false")}
//}}";
        }

        private static LlmDecision? ParseDecision(string text)
        {
            try
            {
                var json = ExtractJson(text);

                return JsonSerializer.Deserialize<LlmDecision>(
                    json,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
            }
            catch
            {
                return null;
            }
        }

        private static string ExtractJson(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "{}";

            text = text.Trim();

            if (text.StartsWith("```"))
            {
                var firstNewline = text.IndexOf('\n');
                if (firstNewline >= 0)
                    text = text[(firstNewline + 1)..];

                var lastFence = text.LastIndexOf("```", StringComparison.Ordinal);
                if (lastFence >= 0)
                    text = text[..lastFence];
            }

            return text.Trim();
        }

        private static bool InvokeIfValid(ServiceNode node, string? functionName)
        {
            if (string.IsNullOrWhiteSpace(functionName) ||
                string.Equals(functionName, "null", StringComparison.OrdinalIgnoreCase))
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
    }
}