using Microsoft.AspNetCore.Mvc.Authorization;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Collections.Generic;
using System.Linq;
using zipkin4net;

namespace Microsoft.eShopOnContainers.Services.Basket.API.Auth.Server
{
    public class AuthorizationHeaderParameterOperationFilter : IOperationFilter
    {
        private zipkin4net.Trace trace;
        public AuthorizationHeaderParameterOperationFilter()
        {
            trace = zipkin4net.Trace.Create();
        }

        public void Apply(Operation operation, OperationFilterContext context)
        {
            trace.Record(Annotations.ServerRecv( ));

            var filterPipeline = context.ApiDescription.ActionDescriptor.FilterDescriptors;
            var isAuthorized = filterPipeline.Select(filterInfo => filterInfo.Filter).Any(filter => filter is AuthorizeFilter);
            var allowAnonymous = filterPipeline.Select(filterInfo => filterInfo.Filter).Any(filter => filter is IAllowAnonymousFilter);

            if (isAuthorized && !allowAnonymous)
            {
                if (operation.Parameters == null)
                    operation.Parameters = new List<IParameter>();

                operation.Parameters.Add(new NonBodyParameter
                {
                    Name = "Authorization",
                    In = "header",
                    Description = "access token",
                    Required = true,
                    Type = "string"
                });
            }
        }
    }
}
