﻿using Microsoft.AspNetCore.Authorization;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Collections.Generic;
using System.Linq;
using zipkin4net;

namespace Basket.API.Infrastructure.Filters
{
    public class AuthorizeCheckOperationFilter : IOperationFilter
    {
        private zipkin4net.Trace trace;
        public AuthorizeCheckOperationFilter()
        {
            trace = zipkin4net.Trace.Create();
        }

        public void Apply(Operation operation, OperationFilterContext context)
        {
            trace.Record(Annotations.ServiceName("AuthorizeCheckOperationFilter:Apply"));
            trace.Record(Annotations.ServerRecv());
            // Check for authorize attribute
            var hasAuthorize = context.ApiDescription.ControllerAttributes().OfType<AuthorizeAttribute>().Any() ||
                               context.ApiDescription.ActionAttributes().OfType<AuthorizeAttribute>().Any();

            if (hasAuthorize)
            {
                operation.Responses.Add("401", new Response { Description = "Unauthorized" });
                operation.Responses.Add("403", new Response { Description = "Forbidden" });

                operation.Security = new List<IDictionary<string, IEnumerable<string>>>();
                operation.Security.Add(new Dictionary<string, IEnumerable<string>>
                {
                    { "oauth2", new [] { "basketapi" } }
                });
            }
            trace.Record(Annotations.ServerSend());
        }
    }
}