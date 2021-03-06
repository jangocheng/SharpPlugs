﻿using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SharpPlug.Core;
using SharpPlug.WebApi.Configuration;

namespace SharpPlug.WebApi.Router
{
    public static class ApiRouterSharpBuilderExtensions
    {
        /// <summary>
        /// Auto Add RouterAttribute And  HttpMethodAttribute
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="setupAction"></param>
        /// <returns></returns>
        public static ISharpPlugBuilder AddWebApiRouter(this ISharpPlugBuilder builder, Action<SharpPlugRouterOptions> setupAction = null)
        {
            var options = new SharpPlugRouterOptions()
            {
                CustomRule = new Dictionary<string, HttpVerbs>()
            };
            setupAction?.Invoke(options);
            builder.Services.Configure<SharpPlugRouterOptions>(opt =>
            {
                opt.CustomRule = options.CustomRule;
            });
            builder.Services.Configure<MvcOptions>(c =>
                c.Conventions.Add(new SharpPlugRouterActionModelConvention(options)));
            return builder;
        }
    }
}
