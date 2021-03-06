using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Vertica.Integration.Infrastructure.Logging.Loggers
{
	internal class EventLogger : Logger
    {
        private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

	    private readonly string _sourceName;

	    public EventLogger(EventLoggerConfiguration configuration, IRuntimeSettings runtimeSettings)
	    {
		    if (configuration == null) throw new ArgumentNullException(nameof(configuration));
		    if (runtimeSettings == null) throw new ArgumentNullException(nameof(runtimeSettings));

		    _sourceName = configuration.SourceName ?? IntegrationService(runtimeSettings);
	    }

		private static string IntegrationService(IRuntimeSettings runtimeSettings)
		{
			ApplicationEnvironment environment = runtimeSettings.Environment;

			return string.Concat("Integration Service",
				environment != null ? $" [{environment}]" : string.Empty);
		}

		protected override string Insert(TaskLog log)
        {
            return GenerateEventId().ToString();
        }

        protected override string Insert(MessageLog log)
        {
            return null;
        }

        protected override string Insert(StepLog log)
        {
            return null;
        }

        protected override string Insert(ErrorLog log)
        {
            int id = GenerateEventId();

            string message = string.Join(Environment.NewLine,
                log.MachineName,
                log.IdentityName,
                log.CommandLine,
                log.Severity,
                log.Target,
                log.TimeStamp,
				string.Empty,
                "---- BEGIN LOG",
				string.Empty,
                log.Message,
				string.Empty,
                log.FormattedMessage);

            EventLog.WriteEntry(
                _sourceName, 
                message, 
                log.Severity == Severity.Error ? EventLogEntryType.Error : EventLogEntryType.Warning, 
                id);

            return id.ToString();
        }

        protected override void Update(TaskLog log)
        {
            var sb = new StringBuilder();

            sb.AppendLine(string.Join(Environment.NewLine,
                log.MachineName,
                log.IdentityName,
                log.CommandLine,
				string.Empty,
                "---- BEGIN LOG"));

            IEnumerable<LogEntry> entries = new LogEntry[] { log }
                .Concat(log.Messages)
                .Concat(log.Steps)
                .Concat(log.Steps.SelectMany(s => s.Messages))
                .OrderBy(x => x.TimeStamp);

            foreach (LogEntry entry in entries)
            {
                var messageLog = entry as MessageLog;

                sb.Append(Line(entry, messageLog != null ? messageLog.Message : ExecutionTime(entry)));

                ErrorLog error = CheckGetError(entry);

                if (error != null)
                    sb.Append(ErrorLine(error, entry.ToString()));
            }

            EventLog.WriteEntry(
                _sourceName,
                sb.ToString(),
                EventLogEntryType.Information,
				int.Parse(log.Id));
        }

        protected override void Update(StepLog log)
        {
        }

        // http://stackoverflow.com/questions/951702/unique-eventid-generation
        private static int GenerateEventId()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Environment.StackTrace);

            StackTrace trace = new StackTrace();
            
            foreach (StackFrame frame in trace.GetFrames() ?? new StackFrame[0])
            {
                sb.Append(frame.GetILOffset());
                sb.Append(",");
            }

            return sb.ToString().GetHashCode() & 0xFFFF;
        }

        private ErrorLog CheckGetError(LogEntry log)
        {
            var reference = log as IReferenceErrorLog;

            return reference?.ErrorLog;
        }

        private string Line(LogEntry log, string text = null, params object[] args)
        {
            if (!string.IsNullOrWhiteSpace(text))
                text = string.Concat(" ", args != null && args.Length > 0 ? string.Format(text, args) : text);

            return Line(log.TimeStamp, $"[{log}]{text}");
        }

        private string Line(DateTimeOffset timestamp, string text, params object[] args)
        {
            return string.Concat(Environment.NewLine,
	            $"[{timestamp.LocalDateTime:HH:mm:ss}] {(args != null && args.Length > 0 ? string.Format(text, args) : text)}");
        }

        private string ExecutionTime(LogEntry log)
        {
            return $"(Execution time: {log.ExecutionTimeSeconds.GetValueOrDefault().ToString(English)} second(s))";
        }

        private string ErrorLine(ErrorLog error, string name)
        {
            return Line(error.TimeStamp, "[{0}] [{1}]: {2} (ID: {3})",
                name,
                error.Severity,
                error.Message,
                error.Id);
        }
    }
}