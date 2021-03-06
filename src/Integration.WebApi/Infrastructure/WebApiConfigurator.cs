using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web.Http;
using System.Web.Http.Dependencies;
using System.Web.Http.Dispatcher;
using System.Web.Http.ExceptionHandling;
using Castle.MicroKernel;
using Castle.MicroKernel.Lifestyle;
using Microsoft.Owin.BuilderProperties;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Owin;
using Vertica.Integration.Infrastructure.Logging;
using Vertica.Integration.WebApi.Infrastructure.Castle.Windsor;
using IDependencyResolver = System.Web.Http.Dependencies.IDependencyResolver;

namespace Vertica.Integration.WebApi.Infrastructure
{
    internal static class WebApiConfigurator
    {
        public static void Configure(this IAppBuilder app, AppProperties properties, IKernel kernel, Action<IOwinConfiguration> configuration)
        {
            if (app == null) throw new ArgumentNullException(nameof(app));
            if (kernel == null) throw new ArgumentNullException(nameof(kernel));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            HttpConfiguration httpConfiguration = new HttpConfiguration();
            configuration(new OwinConfiguration(app, properties, httpConfiguration, kernel));

            ConfigureJson(httpConfiguration);
            ConfigureServices(kernel, httpConfiguration);

            MapRoutes(httpConfiguration);

            app.UseWebApi(httpConfiguration);
        }

        private static void ConfigureServices(IKernel kernel, HttpConfiguration configuration)
        {
            var resolver = new CustomResolver(() => GetControllerTypes(kernel));

            configuration.Services.Replace(typeof(IAssembliesResolver), resolver);
            configuration.Services.Replace(typeof(IHttpControllerTypeResolver), resolver);

            configuration.Services.Add(typeof(IExceptionLogger), new ExceptionLogger(kernel.Resolve<ILogger>()));

            configuration.DependencyResolver = new CustomDependencyResolver(kernel, configuration.DependencyResolver);
        }

        private static ICollection<Type> GetControllerTypes(IKernel kernel)
        {
            return kernel.Resolve<IWebApiControllers>().Controllers;
        }

        private static void MapRoutes(HttpConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            if (!configuration.Routes.ContainsKey("MS_attributerouteWebApi"))
                configuration.MapHttpAttributeRoutes();

            if (!configuration.Routes.ContainsKey("WebApi"))
            {
                configuration.Routes.MapHttpRoute(
                    name: "WebApi",
                    routeTemplate: "{controller}",
                    defaults: new { controller = "Home" });
            }
        }

        private static void ConfigureJson(HttpConfiguration configuration)
        {
            JsonSerializerSettings json =
                configuration.Formatters.JsonFormatter.SerializerSettings;

            json.Formatting = Formatting.Indented;
            json.Converters.Add(new StringEnumConverter());
        }

        private class OwinConfiguration : IOwinConfiguration
        {
            internal OwinConfiguration(IAppBuilder app, AppProperties properties, HttpConfiguration http, IKernel kernel)
            {
                if (app == null) throw new ArgumentNullException(nameof(app));
                if (http == null) throw new ArgumentNullException(nameof(http));
                if (kernel == null) throw new ArgumentNullException(nameof(kernel));

                App = app;
                Properties = properties;
                Http = http;
                Kernel = kernel;
            }

            public IAppBuilder App { get; }
            public AppProperties Properties { get; }
            public HttpConfiguration Http { get; }
            public IKernel Kernel { get; }
        }

        private class CustomResolver : IAssembliesResolver, IHttpControllerTypeResolver
        {
            private readonly Func<ICollection<Type>> _controllerTypes;

            public CustomResolver(Func<ICollection<Type>> controllerTypes)
            {
                _controllerTypes = controllerTypes;
            }

            public ICollection<Assembly> GetAssemblies()
            {
                return new List<Assembly>();
            }

            public ICollection<Type> GetControllerTypes(IAssembliesResolver assembliesResolver)
            {
                return _controllerTypes();
            }
        }

        private class CustomDependencyResolver : IDependencyResolver
        {
            private readonly IKernel _kernel;
            private readonly IDependencyResolver _currentResolver;

            public CustomDependencyResolver(IKernel kernel, IDependencyResolver currentResolver)
            {
                _kernel = kernel;
                _currentResolver = currentResolver;
            }

            public void Dispose()
            {
                _currentResolver.Dispose();
            }

            public IDependencyScope BeginScope()
            {
                return new CustomDependencyScope(_kernel, _currentResolver);
            }

            public object GetService(Type serviceType)
            {
                bool hasComponent = _kernel.HasComponent(serviceType);

                return hasComponent
                    ? _kernel.Resolve(serviceType)
                    : _currentResolver.GetService(serviceType);
            }

            public IEnumerable<object> GetServices(Type serviceType)
            {
                bool hasComponent = _kernel.HasComponent(serviceType);

                return hasComponent
                    ? _kernel.ResolveAll(serviceType).OfType<object>().ToArray()
                    : _currentResolver.GetServices(serviceType);
            }
        }

        private class CustomDependencyScope : IDependencyScope
        {
            private readonly IKernel _kernel;

            private readonly IDisposable _windsorScope;
            private readonly IDependencyScope _currentResolverScope;

            public CustomDependencyScope(IKernel kernel, IDependencyResolver currentResolver)
            {
                if (kernel == null) throw new ArgumentNullException(nameof(kernel));
                if (currentResolver == null) throw new ArgumentNullException(nameof(currentResolver));

                _kernel = kernel;

                _windsorScope = kernel.BeginScope();
                _currentResolverScope = currentResolver.BeginScope();
            }

            public void Dispose()
            {
                _currentResolverScope.Dispose();
                _windsorScope.Dispose();
            }

            public object GetService(Type serviceType)
            {
                bool hasComponent = _kernel.HasComponent(serviceType);

                return hasComponent
                    ? _kernel.Resolve(serviceType)
                    : _currentResolverScope.GetService(serviceType);
            }

            public IEnumerable<object> GetServices(Type serviceType)
            {
                bool hasComponent = _kernel.HasComponent(serviceType);

                return hasComponent
                    ? _kernel.ResolveAll(serviceType).OfType<object>()
                    : _currentResolverScope.GetServices(serviceType);
            }
        }
    }
}