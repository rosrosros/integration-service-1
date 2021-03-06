﻿using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Vertica.Integration.Infrastructure.Logging;
using Vertica.Utilities;
using Vertica.Utilities.Extensions.EnumerableExt;

namespace Vertica.Integration.Domain.Monitoring
{
    [Guid("9FF492BF-D4B5-4E67-AF72-C02EA8671051")]
    [Description("Used by the MonitorTask. Remember to set one or more recipients for each of the defined Targets.")]
    public class MonitorConfiguration
    {
        public MonitorConfiguration()
        {
            Targets = new[]
            {
                new MonitorTarget(Target.Service)
            };

            SubjectPrefix = "Integration Service";

            MonitorFolders = new MonitorFoldersConfiguration();
            PingUrls = new PingUrlsConfiguration();
        }

        public DateTimeOffset LastRun { get; set; }
        public bool Disabled { get; set; }

        public string[] IgnoreErrorsWithMessagesContaining { get; set; }
        public string[] MessageGroupingPatterns { get; set; }
        public MonitorTarget[] Targets { get; set; }
        public string SubjectPrefix { get; set; }

        // TODO: Tilføj mulighed for at hvis samme fejl (inkl. group-patterns) sker < threshold, så ignoreres den
        //  - en udvidelse af IgnoreErrorsWithMessagesContaining => IgnoreErrorsWithMessagesContainingWithThreshold

        public MonitorFoldersConfiguration MonitorFolders { get; }
        public PingUrlsConfiguration PingUrls { get; }

        public MonitorTarget EnsureMonitorTarget(ITarget target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));

            var existingTargets = Targets ?? new MonitorTarget[0];

            MonitorTarget monitorTarget = existingTargets.FirstOrDefault(x => x.Equals(target));

            if (monitorTarget == null)
            {
                monitorTarget = new MonitorTarget(target.Name);
                Targets = existingTargets.Concat(new[] { monitorTarget }).ToArray();
            }

            return monitorTarget;
        }

        public void RemoveTarget(ITarget target)
        {
            Targets = Targets?.Where(x => !x.Equals(target)).ToArray();
        }

        internal void Assert()
        {
            if (Targets == null)
                throw new InvalidOperationException("No targets defined for MonitorConfiguration.");

            MonitorTarget service = Targets.SingleOrDefault(x => x.Equals(Target.Service));

            if (service == null)
                throw new InvalidOperationException($"Missing required target '{Target.Service}' for MonitorConfiguration.");
        }

        public class MonitorFoldersConfiguration
        {
            public MonitorFoldersConfiguration()
            {
                Enabled = true;
                EnsureFolders();
            }

            private void EnsureFolders()
            {
                if (Folders == null)
                    Folders = new Folder[0];
            }

            public Folder this[int index]
            {
                get
                {
                    EnsureFolders();
                    return Folders[index];
                }
            }

            public bool Enabled { get; set; }
            public Folder[] Folders { get; set; }

            public Folder[] GetEnabledFolders()
            {
                EnsureFolders();
                return Folders.Where(x => Enabled && x.Enabled && !string.IsNullOrWhiteSpace(x.Path) && x.Criteria != null).ToArray();
            }

            public class Folder
            {
                public Folder()
                {
                    Enabled = true;
                    Target = Target.Service;
                }

                public bool Enabled { get; set; }
                public string Path { get; set; }
                public string SearchPattern { get; set; }
                public bool IncludeSubDirectories { get; set; }
                public FileCriteria Criteria { get; set; }
                public Target Target { get; set; }

                public override string ToString()
                {
                    var sb = new StringBuilder();

                    sb.Append(Path);

                    if (SearchPattern != null)
                        sb.AppendFormat(" (pattern = {0})", SearchPattern);

                    if (Criteria != null)
                        sb.AppendFormat(" (criteria = {0})", Criteria);

                    return sb.ToString();
                }
            }

            public void Add(Func<Folder, FileCriterias, FileCriteria> folder)
            {
                if (folder == null) throw new ArgumentNullException(nameof(folder));

                var local = new Folder();
                local.Criteria = folder(local, new FileCriterias());

                EnsureFolders();
                Folders = Folders.Append(local).ToArray();
            }

            public void Remove(Folder folder)
            {
                if (folder == null) throw new ArgumentNullException(nameof(folder));

                EnsureFolders();
                Folders = Folders.Except(new[] { folder }).ToArray();
            }

            public void Clear()
            {
                Folders = new Folder[0];
            }

            public class FileCriterias
            {
                public FileCriteria FilesOlderThan(TimeSpan timeSpan)
                {
                    return new FilesOlderThanCriteria((uint)timeSpan.TotalSeconds);
                }
            }

            public abstract class FileCriteria
            {
                public abstract bool IsSatisfiedBy(string file);
            }

            public class FilesOlderThanCriteria : FileCriteria
            {
                protected FilesOlderThanCriteria()
                {
                }

                internal FilesOlderThanCriteria(uint seconds)
                {
                    Seconds = seconds;
                }

                public override bool IsSatisfiedBy(string file)
                {
                    return (Time.UtcNow - File.GetLastWriteTimeUtc(file)) > TimeSpan.FromSeconds(Seconds);
                }

                public uint Seconds { get; set; }

                public override string ToString()
                {
                    return $"Files older than {Seconds} second(s).";
                }
            }
        }

        public class PingUrlsConfiguration
        {
            public PingUrlsConfiguration()
            {
                Enabled = true;
                MaximumWaitTimeSeconds = (uint)TimeSpan.FromMinutes(2).TotalSeconds;
            }

            public bool Enabled { get; set; }
            public uint MaximumWaitTimeSeconds { get; set; }
            public string[] Urls { get; set; }
            public bool? UseProxy { get; set; }

            internal bool ShouldExecute => Enabled && Urls != null && Urls.Length > 0;
        }
    }
}