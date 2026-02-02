using System.ClientModel;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using Microsoft.Teams.AI.Models.OpenAI;
using Microsoft.Teams.Api.Auth;
using Microsoft.Teams.Apps;
using Microsoft.Teams.Common.Http;
using Microsoft.Teams.Plugins.AspNetCore.DevTools.Extensions;
using Microsoft.Teams.Plugins.AspNetCore.Extensions;
using Preworkinagent;

var builder = WebApplication.CreateBuilder(args);

// Configure Azure OpenAI
var configuration = builder.Configuration;

var azureOpenAIModel = configuration["AzureOpenAIModel"] ??
    throw new InvalidOperationException("AzureOpenAIModel not configured");
var azureOpenAIEndpoint = configuration["AzureOpenAIEndpoint"] ??
    throw new InvalidOperationException("AzureOpenAIEndpoint not configured");
var azureOpenAIKey = configuration["AzureOpenAIKey"] ??
    throw new InvalidOperationException("AzureOpenAIKey not configured");

var azureOpenAI = new AzureOpenAIClient(
    new Uri(azureOpenAIEndpoint),
    new ApiKeyCredential(azureOpenAIKey)
);

var aiModel = new OpenAIChatModel(azureOpenAIModel, azureOpenAI);

// Register the AI model in DI container
builder.Services.AddSingleton(aiModel);



// ////////deployment in teams*******************************************************************//////

// Func<string[], string?, Task<ITokenResponse>> createTokenFactory = async (string[] scopes, string? tenantId) =>
// {
//     var clientId = Environment.GetEnvironmentVariable("CLIENT_ID");
//     var managedIdentityCredential = new ManagedIdentityCredential(clientId);
//     var tokenRequestContext = new TokenRequestContext(scopes, tenantId: tenantId);
//     var accessToken = await managedIdentityCredential.GetTokenAsync(tokenRequestContext);

//     return new TokenResponse
//     {
//         TokenType = "Bearer",
//         AccessToken = accessToken.Token,
//     };
// };

// var appBuilder = App.Builder()
//    .AddCredentials(new TokenCredentials(
//        Environment.GetEnvironmentVariable("CLIENT_ID") ?? string.Empty,
//        async (tenantId, scopes) =>
//        {
//            return await createTokenFactory(scopes, tenantId);
//        }
//    ));

// builder.AddTeams(appBuilder);
// builder.AddTeamsDevTools();

////////deployment in teams*******************************************************************//////

////////local running*******************************************************************//////
///
/// 
builder.AddTeams();
builder.AddTeamsDevTools();

/// 
////////local running*******************************************************************//////

// Register AI Agent Function Classes
builder.Services.AddSingleton<Preworkinagent.Functions.PreWorkInSearchFunctions>();
builder.Services.AddSingleton<Preworkinagent.Functions.JobDetailsFunctions>();
builder.Services.AddSingleton<Preworkinagent.Functions.RFIInitiateFunctions>();
builder.Services.AddSingleton<Preworkinagent.Functions.RFIManagementFunctions>();
builder.Services.AddSingleton<Preworkinagent.Functions.RFIStatusFunctions>();

// Register Adaptive Card Handler
builder.Services.AddSingleton<Preworkinagent.Cards.CardActionHandler>();

builder.Services.AddSingleton<MainController>();
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseTeams();
app.Run();