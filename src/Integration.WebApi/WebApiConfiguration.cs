using System;
using System.Collections.Generic;
using System.Reflection;
using System.Web.Http;
using Castle.Windsor;
using Owin;
using Vertica.Integration.Domain.LiteServer;
using Vertica.Integration.WebApi.Infrastructure;
using Vertica.Integration.WebApi.Infrastructure.Castle.Windsor;

namespace Vertica.Integration.WebApi
{
    public class WebApiConfiguration : 
        IInitializable<IWindsorContainer>,
        IInitializable<ApplicationConfiguration>, 
        IInitializable<IApplicationContext>
    {
        private readonly List<Assembly> _scan;
        private readonly List<Type> _add;
        private readonly List<Type> _remove;
	    private readonly HttpServerConfiguration _httpServerConfiguration;

        private IAppBuilder _currentApp;
        private IWindsorContainer _container;

        internal WebApiConfiguration(ApplicationConfiguration application)
        {
            if (application == null) throw new ArgumentNullException(nameof(application));

			Application = application.Hosts(hosts => hosts.Host<WebApiHost>());

            _scan = new List<Assembly>();
            _add = new List<Type>();
            _remove = new List<Type>();

	        _httpServerConfiguration = new HttpServerConfiguration();

            // scan own assembly
            AddFromAssemblyOfThis<WebApiConfiguration>();
        }

        public ApplicationConfiguration Application { get; }

        /// <summary>
        /// Scans the assembly of the defined <typeparamref name="T"></typeparamref> for public classes inheriting <see cref="ApiController"/>.
        /// <para />
        /// </summary>
        /// <typeparam name="T">The type in which its assembly will be scanned.</typeparam>
        public WebApiConfiguration AddFromAssemblyOfThis<T>()
        {
            _scan.Add(typeof (T).Assembly);

            return this;
        }

        /// <summary>
        /// Adds the specified <typeparamref name="TController"/>.
        /// </summary>
        /// <typeparam name="TController">Specifies the <see cref="ApiController"/> to be added.</typeparam>
        public WebApiConfiguration Add<TController>()
            where TController : ApiController
        {
            _add.Add(typeof(TController));

            return this;
        }

        /// <summary>
        /// Skips the specified <typeparamref name="TController" />.
        /// </summary>
        /// <typeparam name="TController">Specifies the <see cref="ApiController"/> that will be skipped.</typeparam>
        public WebApiConfiguration Remove<TController>()
            where TController : ApiController
        {
            _remove.Add(typeof (TController));

            return this;
        }

		/// <summary>
		/// Clears all registred ApiControllers.
		/// </summary>
	    public WebApiConfiguration Clear()
	    {
		    _remove.Clear();
		    _add.Clear();
			_scan.Clear();

		    return this;
	    }

		/// <summary>
		/// Adds WebApi to <see cref="ILiteServerFactory"/> allowing WebApi to run simultaneously with other servers.
		/// </summary>
		public WebApiConfiguration AddToLiteServer()
	    {
		    Application.UseLiteServer(server => server.AddServer<WebApiBackgroundServer>());

		    return this;
	    }

	    public WebApiConfiguration HttpServer(Action<HttpServerConfiguration> httpServer)
	    {
		    if (httpServer == null) throw new ArgumentNullException(nameof(httpServer));

		    httpServer(_httpServerConfiguration);

		    return this;
	    }
        
        /// <summary>
        /// Pass an existing <see cref="IAppBuilder"/> instance to have WebApi configured on that.
        /// </summary>
        public WebApiConfiguration FromCurrentApp(IAppBuilder currentApp)
        {
            if (currentApp == null) throw new ArgumentNullException(nameof(currentApp));

            _currentApp = currentApp;

            return this;
        }

        void IInitializable<IWindsorContainer>.Initialized(IWindsorContainer container)
        {
            _container = container;
        }

        void IInitializable<ApplicationConfiguration>.Initialized(ApplicationConfiguration application)
	    {
	        var installer = new WebApiInstaller(
	            _scan.ToArray(),
	            _add.ToArray(),
	            _remove.ToArray(),
	            _httpServerConfiguration);

            Application
	            .Services(services => services
	                .Advanced(advanced => advanced
	                    .Install(installer)));
	    }

        void IInitializable<IApplicationContext>.Initialized(IApplicationContext context)
        {
            if (_currentApp != null)
                _httpServerConfiguration.Configure(_container.Kernel, _currentApp);
        }
    }
}