// The endpoint for LM Studio's local server
using Esiur.Resource;
using Esiur.Stores;
using Esiur.Tests.Annotations;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.Data;


var endpoint = "http://localhost:1234/v1";
//var endpoint = "http://127.0.0.1:22334/v1";
var credential = new ApiKeyCredential("llm");

var runner = new LlmRunner();

var models = new List<ModelConfig>
{
    new()
    {
        Name = "phi-3-mini-4k-instruct-qnn-npu:2",
        Endpoint = endpoint,
        ApiKey = credential,
        ModelName = "phi-3-mini-4k-instruct-qnn-npu:2"
    },
    new()
    {
        Name = "phi-3.5-mini-instruct-qnn-npu:1",
        Endpoint = endpoint,
        ApiKey = credential,
        ModelName = "phi-3.5-mini-instruct-qnn-npu:1"
    },
    new()
    {
        Name = "qwen2.5-7b-instruct-qnn-npu:2",
        Endpoint = endpoint,
        ApiKey = credential,
        ModelName = "qwen2.5-7b-instruct-qnn-npu:2"
    },
    new()
    {
        Name = "deepseek-r1-distill-qwen-7b-qnn-npu:1",
        Endpoint = endpoint,
        ApiKey = credential,
        ModelName = "deepseek-r1-distill-qwen-7b-qnn-npu:1"
    },
    new()
    {
        Name = "qwen3-4b-2507",
        Endpoint = endpoint,
        ApiKey = credential,
        ModelName = "qwen/qwen3-4b-2507"
    },
    new()
    {
        Name = "gemma-3n-e4b",
        Endpoint = endpoint,
        ApiKey = credential,
        ModelName = "google/gemma-3n-e4b"
    },
    new()
    {
        Name = "qwen2.5-7b-instruct-1m",
        Endpoint = endpoint,
        ApiKey = credential,
        ModelName = "qwen2.5-7b-instruct-1m"
    },


    new()
    {
        Name = "Phi-4",
        Endpoint = endpoint,
        ApiKey = credential,
        ModelName = "microsoft/phi-4"
    },
    new()
    {
        Name = "Qwen2.5-7B",
        Endpoint = endpoint,
        ApiKey = credential,
        ModelName = "qwen2.5-7b-instruct"
    },
    new()
    {
        Name = "gpt-oss",
        Endpoint = endpoint,
        ApiKey = credential,
        ModelName = "openai/gpt-oss-20b"
    },
    new()
    {
        Name = "qwen2.5-1.5b-instruct",
        Endpoint = endpoint,
        ApiKey = credential,
        ModelName = "qwen2.5-1.5b-instruct"
    },
    new()
    {
        Name = "ministral-3-3b",
        Endpoint = endpoint,
        ApiKey = credential,
        ModelName = "mistralai/ministral-3-3b"
    },
    new()
    {
        Name = "deepseek-r1-0528-qwen3-8b",
        Endpoint = endpoint,
        ApiKey = credential,
        ModelName = "deepseek/deepseek-r1-0528-qwen3-8b"
    }
};

var (results, summary) = await runner.RunAsync(models, 
    250);

foreach (var item in summary)
{
    Console.WriteLine($"{item.Model}: Correct={item.CorrectRate:F1}% Repair={item.RepairRate:F1}% Mean={item.MeanLatencyMs:F1} ms P95={item.P95LatencyMs:F1} ms");
}