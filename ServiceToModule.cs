using System;
using System.IO;
using System.Linq;
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
using Uniscale.Core;

namespace Uniscale.Function
{
    public static class ServiceToModule
    {
        private static readonly Dictionary<Guid,UserFull> users = new();
        private static PlatformSession session;

        private static async Task<PlatformSession> getSession() {
            if (session == null) {
                session = await Platform.Builder()
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
            }
            return session;
        }

        [FunctionName("ServiceToModule")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var session = await getSession();
            var data = await session.AcceptGatewayRequest(requestBody);
            return new OkObjectResult(data.ToJson());
        }
    }
}
