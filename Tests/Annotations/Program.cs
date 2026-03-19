// The endpoint for LM Studio's local server
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.Data;

var endpoint = "http://localhost:1234/v1";
var credential = new ApiKeyCredential("lm-studio");

var client = new OpenAIClient(credential, new OpenAIClientOptions() { Endpoint = new Uri(endpoint) });

var chat = client.GetChatClient("microsoft/phi-4");

//List<ChatMessage> messages = new List<ChatMessage>
//{
//    new SystemChatMessage("You are a helpful assistant that only speaks in rhymes."),
//    new UserChatMessage("What is the capital of France?")
//};

//// Send the entire conversation history
//ChatCompletion completion = chat.CompleteChat(messages);

var response = await chat.CompleteChatAsync(
    "Explain what Pi means"
);

Console.WriteLine(response.Value.Content[0].Text);