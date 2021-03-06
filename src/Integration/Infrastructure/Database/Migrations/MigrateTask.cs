﻿using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Text;
using Castle.MicroKernel;
using FluentMigrator;
using FluentMigrator.Runner;
using FluentMigrator.Runner.Announcers;
using FluentMigrator.Runner.Initialization;
using FluentMigrator.Runner.Processors;
using FluentMigrator.Runner.Processors.SqlServer;
using FluentMigrator.Runner.Processors.SQLite;
using Vertica.Integration.Infrastructure.Database.Migrations.Features;
using Vertica.Integration.Infrastructure.Features;
using Vertica.Integration.Infrastructure.Logging;
using Vertica.Integration.Model;

namespace Vertica.Integration.Infrastructure.Database.Migrations
{
#pragma warning disable 618
    public class MigrateTask : Task
#pragma warning restore 618
    {
        private readonly IKernel _kernel;
        private readonly IFeatureToggler _featureToggler;

        private readonly MigrationDb[] _dbs;
        private readonly bool _databaseCreated;
        private readonly FeatureAttribute[] _disabledFeatures;

        public MigrateTask(Lazy<IDbFactory> db, IIntegrationDatabaseConfiguration configuration, IKernel kernel, IMigrationDbs dbs, IFeatureToggler featureToggler)
        {
            _kernel = kernel;
            _featureToggler = featureToggler;

	        if (!configuration.Disabled)
	        {
		        string connectionString = EnsureIntegrationDb(db.Value, configuration.CheckExistsAndCreateDatabaseIfNotFound, out _databaseCreated);

		        var integrationDb = new IntegrationMigrationDb(
			        configuration.DatabaseServer,
			        ConnectionString.FromText(connectionString),
			        typeof (M1_Baseline).Assembly,
			        typeof (M1_Baseline).Namespace);

                MigrationRunner runner = CreateRunner(integrationDb, out _);

	            long latestVersion = runner.VersionLoader.VersionInfo.Latest();
	            _disabledFeatures = DisableFeatures(latestVersion);

		        dbs = dbs.WithIntegrationDb(integrationDb);
	        }

	        _dbs = dbs.ToArray();
        }

        private FeatureAttribute[] DisableFeatures(long laterThanVersion)
        {
            // we need to disable database related features like logging, distributed mutex and such - when upgrading our own schema
            return
                typeof(M1_Baseline).Assembly.GetTypes()
                    .Where(x =>
                        x.IsSubclassOf(typeof(Migration)) && 
                        !x.IsAbstract &&
                        x.Namespace == typeof(M1_Baseline).Namespace)
                    .Where(x =>
                    {
                        var migration = x.GetCustomAttribute<MigrationAttribute>();

                        return (migration?.Version ?? -1) > laterThanVersion;
                    })
                    .SelectMany(x => x.GetCustomAttributes<FeatureAttribute>())
                    .Select(x =>
                    {
                        x.Disable(_featureToggler);

                        return x;
                    })
                    .ToArray();
        }

        public override string Description => "Runs migrations against all configured databases. Will also execute any custom task if provided by Arguments.";

	    public override void StartTask(ITaskExecutionContext context)
        {
	        MigrationDb[] destinations = _dbs;

            // if specific migration dbs have been provided by argument, only they'll be run
			string[] names = (context.Arguments["Names"] ?? string.Empty)
				.Split(new[] { ",", ";" }, StringSplitOptions.RemoveEmptyEntries);

	        if (names.Length > 0)
	        {
		        ILookup<string, MigrationDb> migrationsByName = 
					_dbs.ToLookup(db => db.IdentifyingName, db => db, StringComparer.OrdinalIgnoreCase);

				destinations = names
					.SelectMany(x => migrationsByName[x])
					.ToArray();
	        }

	        string action = (context.Arguments["Action"] ?? string.Empty).ToLowerInvariant();

			foreach (MigrationDb destination in destinations)
            {
                context.ThrowIfCancelled();

                MigrationRunner runner = CreateRunner(destination, out StringBuilder output);

	            switch (action)
	            {
					case "list":
			            destination.List(runner, context, _kernel);
			            break;
					case "rollback":
			            destination.Rollback(runner, context, _kernel);
			            break;
		            default:
			            destination.MigrateUp(runner, context, _kernel);
			            break;
	            }
                
				runner.Processor.Dispose();
				
                if (output.Length > 0)
                    context.Log.Message(output.ToString());

                if (_disabledFeatures != null)
                {
                    foreach (FeatureAttribute feature in _disabledFeatures)
                        feature.Enable(_featureToggler);
                }
            }

            if (_databaseCreated)
            {
                context.Log.Warning(
                    Target.Service,
                    "Created new database (using Simple Recovery) and applied migrations to this. Make sure to configure this new database (auto growth, backup etc).");
            }
        }

        private static string EnsureIntegrationDb(IDbFactory db, bool checkExistsAndCreateIntegrationDbIfNotFound, out bool databaseCreated)
        {
            using (IDbConnection connection = db.GetConnection())
            {
                if (!checkExistsAndCreateIntegrationDbIfNotFound)
                {
                    databaseCreated = false;
                    return connection.ConnectionString;
                }

                using (IDbCommand command = connection.CreateCommand())
                {
                    string databaseName = connection.Database;

                    var builder = new SqlConnectionStringBuilder(connection.ConnectionString);

                    string ChangeDatabase(string dbName)
                    {
                        builder["Initial Catalog"] = dbName;
                        return builder.ConnectionString;
                    }

                    connection.ConnectionString = ChangeDatabase("master");
                    connection.Open();

                    command.CommandText = @"
IF NOT EXISTS (SELECT name FROM master.dbo.sysdatabases WHERE name = @DbName)
	BEGIN

		EXECUTE ('
			CREATE DATABASE ' + @DbName + ';
			ALTER DATABASE ' + @DbName +' SET RECOVERY SIMPLE
		')
		
		SELECT 'CREATED'
	END
ELSE
	SELECT 'EXISTS'
";

                    IDbDataParameter parameter = command.CreateParameter();
                    parameter.ParameterName = "DbName";
                    parameter.Value = databaseName;

                    command.Parameters.Add(parameter);

                    databaseCreated = (string)command.ExecuteScalar() == "CREATED";

                    return ChangeDatabase(databaseName);
                }                
            }
        }

        private MigrationRunner CreateRunner(MigrationDb db, out StringBuilder output)
        {
            var sb = output = new StringBuilder();

            var announcer = new TextWriterAnnouncer(s =>
            {
                if (sb.Length == 0)
                    sb.AppendLine();

                sb.Append(s);
            });

            IMigrationProcessorFactory factory = CreateFactory(db.DatabaseServer);
            IMigrationProcessor processor = factory.Create(db.ConnectionString, announcer, new MigrationOptions(db));

            var context = new RunnerContext(announcer)
            {
                Namespace = db.NamespaceContainingMigrations,
                ApplicationContext = _kernel
            };

            return new MigrationRunner(db.Assembly, context, processor);
        }

        private static IMigrationProcessorFactory CreateFactory(DatabaseServer databaseServer)
        {
            switch (databaseServer)
            {
                case DatabaseServer.SqlServer2012:
                    return new SqlServer2012ProcessorFactory();
                case DatabaseServer.SqlServer2014:
                    return new SqlServer2014ProcessorFactory();
				case DatabaseServer.Sqlite:
					return new SQLiteProcessorFactory();
                default:
                    throw new ArgumentOutOfRangeException(nameof(databaseServer));
            }
        }

        private class MigrationOptions : IMigrationProcessorOptions
        {
            public MigrationOptions(MigrationDb db)
            {
                PreviewOnly = false;
                ProviderSwitches = null;
                Timeout = db.Timeout;
            }

            public bool PreviewOnly { get; }
            public string ProviderSwitches { get; }
            public int Timeout { get; }
        }
    }
}