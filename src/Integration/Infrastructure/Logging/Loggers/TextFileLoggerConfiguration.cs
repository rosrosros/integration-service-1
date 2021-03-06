﻿using System;
using System.Globalization;
using System.IO;
using System.Threading;

namespace Vertica.Integration.Infrastructure.Logging.Loggers
{
    public class TextFileLoggerConfiguration
    {
        private Organizer _organizer;

	    internal TextFileLoggerConfiguration(LoggingConfiguration logging)
	    {
	        if (logging == null) throw new ArgumentNullException(nameof(logging));

            Logging = logging.Change(l => l
                .Application
                    .Services(services => services
                        .Advanced(advanced => advanced
                            .Register(kernel => this))));
        }

        public LoggingConfiguration Logging { get; }

        public TextFileLoggerConfiguration OrganizeSubFoldersBy(Func<BasedOn, Organizer> basedOn)
        {
            _organizer = basedOn(new BasedOn());

            return this;
        }

		internal FileInfo GetFilePath(TaskLog log, string baseDirectory)
        {
            if (log == null) throw new ArgumentNullException(nameof(log));

            return Combine(baseDirectory, log.TimeStamp, "{0}", log.Name);
        }

		internal FileInfo GetFilePath(ErrorLog log, string baseDirectory)
        {
            if (log == null) throw new ArgumentNullException(nameof(log));

            return Combine(baseDirectory, log.TimeStamp, "{0}-{1}", log.Severity, log.Target);
        }

        private FileInfo Combine(string baseDirectory, DateTimeOffset timestamp, string postFixFormat, params object[] args)
        {
            // Sleep because we use timestamp part of our filename.
            Thread.Sleep(1);

            string fileName = $"{timestamp.LocalDateTime:yyyyMMddHHmmss-fff}-{string.Format(postFixFormat, args)}.txt";

            string subdirectory = _organizer?.SubdirectoryName(timestamp);

            return new FileInfo(Path.Combine(baseDirectory, subdirectory ?? string.Empty, fileName));
        }

        public class BasedOn
        {
            public Organizer Daily => new DailyOrganizer();
	        public Organizer Weekly => new WeeklyOrganizer();
	        public Organizer Monthly => new MonthlyOrganizer();

	        public Organizer Custom(Organizer organizer)
            {
                if (organizer == null) throw new ArgumentNullException(nameof(organizer));

                return organizer;
            }
        }

        public abstract class Organizer
        {
            public abstract string SubdirectoryName(DateTimeOffset date);
        }

        public class DailyOrganizer : Organizer
        {
            public override string SubdirectoryName(DateTimeOffset date)
            {
                return date.LocalDateTime.ToString("yyyyMMdd");
            }
        }

        public class WeeklyOrganizer : Organizer
        {
            public override string SubdirectoryName(DateTimeOffset date)
            {
                int weekNumber = CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(
                    date.LocalDateTime,
                    CalendarWeekRule.FirstFourDayWeek,
                    DayOfWeek.Monday);

                return $"{date.LocalDateTime:yyyy}-{weekNumber:00}";
            }
        }

        public class MonthlyOrganizer : Organizer
        {
            public override string SubdirectoryName(DateTimeOffset date)
            {
                return date.LocalDateTime.ToString("yyyyMM");
            }
        }
    }
}