﻿using System;
using Castle.MicroKernel.Registration;
using Vertica.Integration.Infrastructure;
using Vertica.Integration.Infrastructure.Database;
using Vertica.Integration.Infrastructure.Database.Migrations;
using Vertica.Integration.Infrastructure.Factories.Castle.Windsor.Installers;
using Vertica.Integration.Infrastructure.Logging;
using Vertica.Integration.Model;
using Vertica.Integration.Model.Hosting;

namespace Vertica.Integration
{
	public class ApplicationConfiguration
    {
		private readonly ExtensibilityConfiguration _extensibility;

	    private readonly DatabaseConfiguration _database;
	    private readonly ServicesConfiguration _services;
	    private readonly HostsConfiguration _hosts;
	    private readonly TasksConfiguration _tasks;
		private readonly LoggingConfiguration _logging;
		private readonly MigrationConfiguration _migration;
        private readonly EnvironmentConfiguration _environment;

		internal ApplicationConfiguration()
        {
		    _extensibility = new ExtensibilityConfiguration();

		    _database = Register(() => new DatabaseConfiguration(this));
		    _services = Register(() => new ServicesConfiguration(this));
		    _hosts = Register(() => new HostsConfiguration(this));
		    _tasks = Register(() => new TasksConfiguration(this));
            _logging = Register(() => new LoggingConfiguration(this));
            _migration = Register(() => new MigrationConfiguration(this));
            _environment = Register(() => new EnvironmentConfiguration(this));

			Register(() => this);
        }

	    public ApplicationConfiguration Services(Action<ServicesConfiguration> services)
	    {
	        if (services == null) throw new ArgumentNullException(nameof(services));

	        services(_services);

            return this;
        }

	    public ApplicationConfiguration Database(Action<DatabaseConfiguration> database)
	    {
	        if (database == null) throw new ArgumentNullException(nameof(database));

	        database(_database);

	        return this;
	    }

	    public ApplicationConfiguration Hosts(Action<HostsConfiguration> hosts)
		{
		    if (hosts == null) throw new ArgumentNullException(nameof(hosts));

		    hosts(_hosts);

			return this;
		}

	    public ApplicationConfiguration Tasks(Action<TasksConfiguration> tasks)
        {
            if (tasks == null) throw new ArgumentNullException(nameof(tasks));

            tasks(_tasks);

			return this;
        }

	    public ApplicationConfiguration Logging(Action<LoggingConfiguration> logging)
        {
            if (logging == null) throw new ArgumentNullException(nameof(logging));

            logging(_logging);

			return this;
        }

	    public ApplicationConfiguration Migration(Action<MigrationConfiguration> migration)
        {
            if (migration == null) throw new ArgumentNullException(nameof(migration));

            migration(_migration);

			return this;
        }

        [Obsolete("Use .Services(services => services.Advanced(advanced => advanced...)) to replace this configuration.")]
        public ApplicationConfiguration Advanced(Action<ServicesAdvancedConfiguration> advanced)
        {
            if (advanced != null)
                Services(services => services.Advanced(advanced));

			return this;
		}

		public ApplicationConfiguration Extensibility(Action<ExtensibilityConfiguration> extensibility)
		{
		    if (extensibility == null) throw new ArgumentNullException(nameof(extensibility));

		    extensibility(_extensibility);

			return this;
		}

        public ApplicationConfiguration Environment(Action<EnvironmentConfiguration> environment)
        {
            if (environment == null) throw new ArgumentNullException(nameof(environment));

            environment(_environment);

            return this;
        }

        [Obsolete("Use .Services(services => services.Advanced(advanced => advanced.Install(installer))")]
        public ApplicationConfiguration AddCustomInstaller(IWindsorInstaller installer)
        {
            return AddCustomInstallers(installer);
        }

        [Obsolete("Use .Services(services => services.Advanced(advanced => advanced.Install(installers))")]
        public ApplicationConfiguration AddCustomInstallers(params IWindsorInstaller[] installers)
        {
            Services(services => services.Advanced(advanced => advanced.Install(installers)));

            return this;
        }

        [Obsolete("Use .Services(services => services.RegisterDependency(singletonInstance))")]
        public ApplicationConfiguration RegisterDependency<T>(T singletonInstance) where T : class
        {
            if (singletonInstance == null) throw new ArgumentNullException(nameof(singletonInstance));

            return AddCustomInstaller(new InstanceInstaller<T>(singletonInstance, x => x.LifestyleSingleton()));
        }

        [Obsolete("Use .Services(services => services.Register<IRuntimeSettings, T>())")]
		public ApplicationConfiguration RuntimeSettings<T>()
			where T : class, IRuntimeSettings
		{
			return Advanced(advanced => advanced.Register<IRuntimeSettings, T>());
		}

        [Obsolete("Use .Services(services => services.Register<IRuntimeSettings>(instance))")]
        public ApplicationConfiguration RuntimeSettings<T>(T instance)
			where T : class, IRuntimeSettings
		{
			if (instance == null) throw new ArgumentNullException(nameof(instance));

			return Advanced(advanced => advanced.Register<IRuntimeSettings>(kernel => instance));
		}

	    public ApplicationConfiguration Change(Action<ApplicationConfiguration> change)
	    {
	        change?.Invoke(this);

	        return this;
	    }

	    private T Register<T>(Func<T> factory) where T : class
        {
            T result = null;

            Extensibility(extensibility => result = extensibility.Register(factory));

            return result;
        }
    }
}