using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

using Uniscale.Designtime;
using UniscaleDemo.Account;
using UniscaleDemo.Account.Account;


// Initialize the Uniscale session
var sessionTask = Platform.Builder()
    .WithInterceptors(i => i
        .InterceptRequest(
            Patterns.Account.GetOrRegister.AllRequestUsages,
            Patterns.Account.GetOrRegister.Handle((input, ctx) => {
                // Get the existing user if there is a match on user handle
                var existingUser = users.Values
                    .FirstOrDefault(u => u.Handle == input);
                if (existingUser != null)
                    return existingUser;

                // Create a new user and return it
                var newUserIdentifier = Guid.NewGuid();
                users.Add(newUserIdentifier, new UserFull {
                    UserIdentifier = newUserIdentifier,
                    Handle = input
                });
                return users[newUserIdentifier];
            })
        )
        .InterceptRequest(
            Patterns.Account.LookupUsers.AllRequestUsages,
            Patterns.Account.LookupUsers.Handle((input, ctx) => {
                return input
                    .Where(identifier => users.ContainsKey(identifier))
                    .Select(identifier => users[identifier])
                    .ToList();
            })
        )
        .InterceptRequest(
            Patterns.Account.SearchAllUsers.AllRequestUsages,
            Patterns.Account.SearchAllUsers.Handle((input, ctx) => {
                return users.Values 
                    .Where(u => u.Handle.ToLower().Contains(input.ToLower()))
                    .ToList();
            })
        ))
    .Build();
if (!sessionTask.IsCompleted)
    sessionTask.Wait();
var session = sessionTask.Result;


namespace Uniscale.Function
{
    public static class ServiceToModule
    {

        private static readonly Dictionary<Guid,UserFull> users = new();

        [FunctionName("ServiceToModule")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");


            string name = req.Query["name"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = await session.AcceptGatewayRequest(requestBody);

            name = name ?? data?.name;

            string responseMessage = string.IsNullOrEmpty(name)
                ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
                : $"Hello, {name}. This HTTP triggered function executed successfully.";

            return new OkObjectResult(responseMessage);
        }
    }
}
