﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;
using SharpPlug.WebApi.Configuration;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SharpPlug.WebApi.Swashbuckle
{
    public class SharpPlugSwaggerGenerator : ISwaggerProvider
    {
        private readonly IApiDescriptionGroupCollectionProvider _apiDescriptionsProvider;
        private readonly ISchemaRegistryFactory _schemaRegistryFactory;
        private readonly SwaggerGeneratorSettings _settings;
        private readonly IOptions<SharpPlugRouterOptions> _sharpPlugRouteroptions;
        //private readonly SwaggerGeneratorOptions _options;
        public SharpPlugSwaggerGenerator(IApiDescriptionGroupCollectionProvider apiDescriptionsProvider, ISchemaRegistryFactory schemaRegistryFactory, SwaggerGeneratorSettings settings, IOptions<SharpPlugRouterOptions> sharpPlugRouteroptions)
        {
            _apiDescriptionsProvider = apiDescriptionsProvider;
            _schemaRegistryFactory = schemaRegistryFactory;
            _sharpPlugRouteroptions = sharpPlugRouteroptions;
            _settings = settings ?? new SwaggerGeneratorSettings();
        }



        public SwaggerDocument GetSwagger(
             string documentName,
             string host = null,
             string basePath = null,
             string[] schemes = null)
        {
            //if (!_sharpPlugRouteroptions.SwaggerDocs.TryGetValue(documentName, out Info info))

            //    throw new UnknownSwaggerDocument(documentName);



            //var applicableApiDescriptions = _apiDescriptionsProvider.ApiDescriptionGroups.Items

            //    .SelectMany(group => group.Items)

            //    .Where(apiDesc => _sharpPlugRouteroptions.DocInclusionPredicate(documentName, apiDesc))

            //    .Where(apiDesc => !_sharpPlugRouteroptions.IgnoreObsoleteActions || !apiDesc.IsObsolete());



            //var schemaRegistry = _schemaRegistryFactory.Create();



            //var swaggerDoc = new SwaggerDocument

            //{

            //    Info = info,

            //    Host = host,

            //    BasePath = basePath,

            //    Schemes = schemes,

            //    Paths = CreatePathItems(applicableApiDescriptions, schemaRegistry),

            //    Definitions = schemaRegistry.Definitions,

            //    SecurityDefinitions = _sharpPlugRouteroptions.SecurityDefinitions.Any() ? _sharpPlugRouteroptions.SecurityDefinitions : null,

            //    Security = _sharpPlugRouteroptions.SecurityRequirements.Any() ? _sharpPlugRouteroptions.SecurityRequirements : null

            //};




            //var filterContext = new DocumentFilterContext(

            //    _apiDescriptionsProvider.ApiDescriptionGroups,

            //    applicableApiDescriptions,

            //    schemaRegistry);



            //foreach (var filter in _sharpPlugRouteroptions.DocumentFilters)

            //{

            //    filter.Apply(swaggerDoc, filterContext);

            //}



            //return swaggerDoc;
            return null;
        }

        public virtual IEnumerable<ApiDescription> ReGenerateApiDes(
            IEnumerable<ApiDescription> apiDescriptions)
        {
            var result = new List<ApiDescription>();
            foreach (var apiDescription in apiDescriptions)
            {
                if (apiDescription.HttpMethod != null)
                    result.Add(apiDescription);
                else
                {
                    if (apiDescription.ActionDescriptor.GetType().GetProperty("ActionName")
                        ?.GetValue(apiDescription.ActionDescriptor) is string actionName)
                    {
                        string httpMethod;
                        foreach (var custom in _sharpPlugRouteroptions.Value.CustomRule)
                        {
                            if (actionName.StartsWith(custom.Key))
                            {
                                switch (custom.Value)
                                {
                                    case HttpVerbs.HttpGet:
                                        httpMethod = "GET";
                                        break;
                                    case HttpVerbs.HttpPost:
                                        httpMethod = "POST";
                                        break;
                                    case HttpVerbs.HttpDelete:
                                        httpMethod = "DELETE";
                                        break;
                                    case HttpVerbs.HttpPut:
                                        httpMethod = "PUT";
                                        break;
                                    default:
                                        httpMethod = "POST";
                                        break;
                                }

                                goto @break;
                            }
                        }
                        if (actionName.StartsWith("Get"))
                            httpMethod = "GET";
                        else if (actionName.StartsWith("Post") || actionName.StartsWith("Add"))
                            httpMethod = "POST";
                        else if (actionName.StartsWith("Update") || actionName.StartsWith("Put"))
                            httpMethod = "PUT";
                        else if (actionName.StartsWith("Del") || actionName.StartsWith("Delete"))
                            httpMethod = "DELETE";
                        else
                            httpMethod = "POST";

                        @break:
                        apiDescription.HttpMethod = httpMethod;
                        result.Add(apiDescription);
                    }
                }
            }

            return result;
        }


        private PathItem CreatePathItem(IEnumerable<ApiDescription> apiDescriptions, ISchemaRegistry schemaRegistry)
        {
            var pathItem = new PathItem();
            var newApiDescriptions = ReGenerateApiDes(apiDescriptions);
            // Group further by http method
            var perMethodGrouping = newApiDescriptions
                .GroupBy(apiDesc => apiDesc.HttpMethod);
            foreach (var group in perMethodGrouping)
            {

                var httpMethod = group.Key;

                if (httpMethod == null)
                    throw new NotSupportedException(
                        $"Ambiguous HTTP method for action - {@group.First().ActionDescriptor.DisplayName}. " +
                        "Actions require an explicit HttpMethod binding for Swagger");

                if (group.Count() > 1)
                    throw new NotSupportedException(
                        $"HTTP method \"{httpMethod}\" & path \"{@group.First().RelativePathSansQueryString()}\" overloaded by actions - {string.Join(",", @group.Select(apiDesc => apiDesc.ActionDescriptor.DisplayName))}. " +
                        "Actions require unique method/path combination for Swagger");

                var apiDescription = group.Single();

                switch (httpMethod)
                {
                    case "GET":
                        pathItem.Get = CreateOperation(apiDescription, schemaRegistry);
                        break;
                    case "PUT":
                        pathItem.Put = CreateOperation(apiDescription, schemaRegistry);
                        break;
                    case "POST":
                        pathItem.Post = CreateOperation(apiDescription, schemaRegistry);
                        break;
                    case "DELETE":
                        pathItem.Delete = CreateOperation(apiDescription, schemaRegistry);
                        break;
                    case "OPTIONS":
                        pathItem.Options = CreateOperation(apiDescription, schemaRegistry);
                        break;
                    case "HEAD":
                        pathItem.Head = CreateOperation(apiDescription, schemaRegistry);
                        break;
                    case "PATCH":
                        pathItem.Patch = CreateOperation(apiDescription, schemaRegistry);
                        break;
                }
            }

            return pathItem;
        }

        private Operation CreateOperation(ApiDescription apiDescription, ISchemaRegistry schemaRegistry)
        {
            var parameters = apiDescription.ParameterDescriptions
                .Where(paramDesc => paramDesc.Source.IsFromRequest && !paramDesc.IsPartOfCancellationToken())
                .Select(paramDesc => CreateParameter(apiDescription, paramDesc, schemaRegistry))
                .ToList();

            var responses = apiDescription.SupportedResponseTypes
                .DefaultIfEmpty(new ApiResponseType { StatusCode = 200 })
                .ToDictionary(
                    apiResponseType => apiResponseType.StatusCode.ToString(),
                    apiResponseType => CreateResponse(apiResponseType, schemaRegistry)
                 );

            var operation = new Operation
            {
                Tags = new[] { _settings.TagSelector(apiDescription) },
                OperationId = apiDescription.FriendlyId(),
                Consumes = apiDescription.SupportedRequestMediaTypes().ToList(),
                Produces = apiDescription.SupportedResponseMediaTypes().ToList(),
                Parameters = parameters.Any() ? parameters : null, // parameters can be null but not empty
                Responses = responses,
                Deprecated = apiDescription.IsObsolete() ? true : (bool?)null
            };

            //var filterContext = new OperationFilterContext(apiDescription, schemaRegistry);
            //foreach (var filter in _settings.OperationFilters)
            //{
            //    filter.Apply(operation, filterContext);
            //}

            //return operation;
            return null;
        }

        private IParameter CreateParameter(
            ApiDescription apiDescription,
            ApiParameterDescription paramDescription,
            ISchemaRegistry schemaRegistry)
        {
            var location = GetParameterLocation(apiDescription, paramDescription);

            var name = _settings.DescribeAllParametersInCamelCase
                ? paramDescription.Name.ToCamelCase()
                : paramDescription.Name;

            var schema = (paramDescription.Type == null) ? null : schemaRegistry.GetOrRegister(paramDescription.Type);

            if (location == "body")
            {
                return new BodyParameter
                {
                    Name = name,
                    Schema = schema
                };
            }

            var nonBodyParam = new NonBodyParameter
            {
                Name = name,
                In = location,
                //Required = (location == "path") || paramDescription.()
            };

            if (schema == null)
                nonBodyParam.Type = "string";
            else
                nonBodyParam.PopulateFrom(schema);

            if (nonBodyParam.Type == "array")
                nonBodyParam.CollectionFormat = "multi";

            return nonBodyParam;
        }

        private string GetParameterLocation(ApiDescription apiDescription, ApiParameterDescription paramDescription)
        {
            if (paramDescription.Source == BindingSource.Form)
                return "formData";
            else if (paramDescription.Source == BindingSource.Body)
                return "body";
            else if (paramDescription.Source == BindingSource.Header)
                return "header";
            else if (paramDescription.Source == BindingSource.Path)
                return "path";
            else if (paramDescription.Source == BindingSource.Query)
                return "query";

            // None of the above, default to "query"
            // Wanted to default to "body" for PUT/POST but ApiExplorer flattens out complex params into multiple
            // params for ALL non-bound params regardless of HttpMethod. So "query" across the board makes most sense
            return "query";
        }

        private Response CreateResponse(ApiResponseType apiResponseType, ISchemaRegistry schemaRegistry)
        {
            var description = ResponseDescriptionMap
                .FirstOrDefault((entry) => Regex.IsMatch(apiResponseType.StatusCode.ToString(), entry.Key))
                .Value;

            return new Response
            {
                Description = description,
                Schema = (apiResponseType.Type != null && apiResponseType.Type != typeof(void))
                    ? schemaRegistry.GetOrRegister(apiResponseType.Type)
                    : null
            };
        }

        private static readonly Dictionary<string, string> ResponseDescriptionMap = new Dictionary<string, string>
        {
            { "1\\d{2}", "Information" },
            { "2\\d{2}", "Success" },
            { "3\\d{2}", "Redirect" },
            { "400", "Bad Request" },
            { "401", "Unauthorized" },
            { "403", "Forbidden" },
            { "404", "Not Found" },
            { "405", "Method Not Allowed" },
            { "406", "Not Acceptable" },
            { "408", "Request Timeout" },
            { "409", "Conflict" },
            { "4\\d{2}", "Client Error" },
            { "5\\d{2}", "Server Error" }
        };
    }
}
