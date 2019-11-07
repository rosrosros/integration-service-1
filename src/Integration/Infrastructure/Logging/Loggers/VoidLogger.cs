﻿namespace Vertica.Integration.Infrastructure.Logging.Loggers
{
	internal class VoidLogger : Logger
    {
        protected override string Insert(TaskLog log)
        {
            return null;
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
            return null;
        }

        protected override void Update(TaskLog log)
        {
        }

        protected override void Update(StepLog log)
        {
        }
    }
}