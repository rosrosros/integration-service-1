using System;
using System.ComponentModel;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Vertica.Integration.Infrastructure.Archiving;
using Vertica.Integration.Infrastructure.Logging;
using Vertica.Utilities.Extensions.StringExt;

namespace Vertica.Integration.Infrastructure.Configuration
{
	public class ConfigurationService : IConfigurationService
	{
		private readonly IConfigurationRepository _repository;
		private readonly IArchiveService _archive;
        private readonly ILogger _logger;
        private readonly JsonSerializerSettings _serializerSettings;

        public ConfigurationService(IArchiveService archive, IConfigurationRepository repository, ILogger logger)
        {
	        _repository = repository;
	        _logger = logger;
	        _archive = archive;

	        _serializerSettings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto
            };
        }

        public TConfiguration Get<TConfiguration>() where TConfiguration : class, new()
        {
            if (typeof (TConfiguration) == typeof(Configuration)) throw new ArgumentException("Getting a Configuration of type Configuration is not allowed.");

            Configuration existing = _repository.Get(GetId<TConfiguration>());

            if (existing != null)
            {
                return JsonConvert.DeserializeObject<TConfiguration>(existing.JsonData, _serializerSettings);
            }

            var configuration = new TConfiguration();
            Save(configuration, "IntegrationService");

            return configuration;
        }

        public TConfiguration Save<TConfiguration>(TConfiguration configuration, string updatedBy, bool createArchiveBackup = false) 
            where TConfiguration : class, new()
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (string.IsNullOrWhiteSpace(updatedBy)) throw new ArgumentException(@"Value cannot be null or empty.", nameof(updatedBy));
            if (configuration is Configuration) throw new ArgumentException(@"Use the specific Save method when saving this Configuration instance.", nameof(configuration));

			string id = GetId<TConfiguration>(warnIfMissingGuid: true);

	        if (createArchiveBackup)
		        Backup(id);

	        Type configurationType = typeof (TConfiguration);
			
	        _repository.Save(new Configuration
            {
	            Id = id,
				Name = configurationType.Name,
				Description = GetDescription(configurationType),
				JsonData = JsonConvert.SerializeObject(configuration, Formatting.Indented, _serializerSettings),
				UpdatedBy = updatedBy
            });

            return Get<TConfiguration>();
        }

		public ArchiveCreated Backup(string id)
		{
			if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException(@"Value cannot be null or empty.", nameof(id));

			Configuration current = _repository.Get(id);

			if (current == null)
				return null;

			return _archive.Archive(current.Name, archive =>
			{
			    archive.Options
			        .GroupedBy("Backup")
			        .ExpiresAfterMonths(1)
			        .Compression(CompressionLevel.Optimal);

				archive.IncludeContent("data", current.JsonData, ".json");
				archive.IncludeContent("meta", string.Join(Environment.NewLine,
					current.Id,
					current.Name,
					current.Description,
					current.Updated.ToString(),
					current.UpdatedBy));
			});
		}

		private static string GetDescription(Type configurationType)
        {
            if (configurationType == null)
                return null;

            DescriptionAttribute attribute = 
                configurationType.GetCustomAttribute<DescriptionAttribute>();

            return attribute?.Description.NullIfEmpty();
        }

        private string GetId<TConfiguration>(bool warnIfMissingGuid = false)
        {
            string id = GetGuidId<TConfiguration>();

            if (id != null)
                return id;

            Type type = typeof (TConfiguration);

            id = string.Join(", ", type.FullName, type.Assembly.GetName().Name);

            if (warnIfMissingGuid)
            {
                _logger.LogWarning(Target.Service,
@"Class '{0}' used for configuration should have been decorated with a [Guid(""[insert-new-Guid-here]"")]-attribute.
This is to ensure a unique and refactor-safe Global ID.

Remember when (or if) you add this Guid-attribute, that you have to (manually) merge the data to the new instance.
If you don't like to do it manually, you can of course use a Migration (inherit from IntegrationMigration and use the MergeConfiguration<T>()-method).

IMPORTANT: Remember to use the ""D"" format for Guids, e.g. 1EB3F675-C634-412F-A76F-FC3F9A4A68D5", id);
            }

            return id;
        }

        internal static string GetGuidId<TConfiguration>()
        {
            Type type = typeof(TConfiguration);

            GuidAttribute attribute = type.GetCustomAttribute<GuidAttribute>();

            Guid guid;
            if (attribute != null && Guid.TryParse(attribute.Value, out guid))
                return guid.ToString("D");

            return null;
        }
    }
}