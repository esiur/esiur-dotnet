// The endpoint for LM Studio's local server
using OpenAI;
using OpenAI.Chat;

using System.Data;

var endpoint = "http://localhost:1234/v1";
var client = new OpenAIClient(new OpenAIClientOptions()
{
    Endpoint = new Uri("http://localhost:1234/v1")
});

var chat = client.GetChatClient("local-model");

var response = await chat.CompleteAsync(
    "Explain what this function does"
);

Console.WriteLine(response.Value.Content[0].Text);