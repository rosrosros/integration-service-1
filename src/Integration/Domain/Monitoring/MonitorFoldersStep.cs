﻿using System;
using System.IO;
using System.Linq;
using System.Text;
using Vertica.Integration.Infrastructure.Extensions;
using Vertica.Integration.Model;
using Vertica.Utilities;

namespace Vertica.Integration.Domain.Monitoring
{
    public class MonitorFoldersStep : Step<MonitorWorkItem>
    {
        public override Execution ContinueWith(ITaskExecutionContext<MonitorWorkItem> context)
        {
            if (context.WorkItem.Configuration.MonitorFolders.GetEnabledFolders().Length == 0)
                return Execution.StepOver;

            return Execution.Execute;
        }

        public override void Execute(ITaskExecutionContext<MonitorWorkItem> context)
        {
            MonitorConfiguration.MonitorFoldersConfiguration.Folder[] folders =
                context.WorkItem.Configuration.MonitorFolders.GetEnabledFolders();

            context.Log.Message(@"Folder(s) monitored:
{0}",
				string.Join(Environment.NewLine, 
                    folders.Select(x => string.Concat(" - ", x.ToString()))));

            foreach (MonitorConfiguration.MonitorFoldersConfiguration.Folder folder in folders)
            {
                string[] files =
                    Directory.EnumerateFiles(folder.Path, folder.SearchPattern ?? "*", folder.IncludeSubDirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                        .Where(folder.Criteria.IsSatisfiedBy)
                        .ToArray();

                if (files.Length > 0)
                {
                    context.Log.Message("{0} file(s) matched by '{1}'.", files.Length, folder);

                    var message = new StringBuilder();
                    message.AppendFormat("{0} file(s) matching criteria: '{1}'.", files.Length, folder.Criteria);
                    message.AppendLine();

                    const int limit = 10;

                    message.AppendLine(string.Join(Environment.NewLine, files
                        .Take(limit)
                        .Select(x => $" - {x}")));

                    if (files.Length > limit)
                        message.AppendLine("...");

                    context.WorkItem.Add(Time.UtcNow, this.Name(), message.ToString(), folder.Target);
                }
            }
        }

        public override string Description => "Monitors a set of configured folders (MonitorConfiguration).";
    }
}