using System;
using System.Collections.Generic;
using Castle.MicroKernel.Registration;
using Vertica.Integration.Infrastructure.Factories.Castle.Windsor.Installers;

namespace Vertica.Integration.Model
{
    public abstract class TaskConfiguration : IEquatable<TaskConfiguration>
    {
        protected TaskConfiguration(ApplicationConfiguration application, Type task)
        {
            if (application == null) throw new ArgumentNullException(nameof(application));
            if (task == null) throw new ArgumentNullException(nameof(task));

            Application = application;
            Task = task;
            Steps = new List<Type>();
        }

        public ApplicationConfiguration Application { get; }

        internal Type Task { get; }
        protected List<Type> Steps { get; }

        internal abstract IWindsorInstaller GetInstaller();

        public bool Equals(TaskConfiguration other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Task == other.Task;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((TaskConfiguration) obj);
        }

        public override int GetHashCode()
        {
            return Task.GetHashCode();
        }
    }

    public sealed class TaskConfiguration<TWorkItem> : TaskConfiguration
    {
        internal TaskConfiguration(ApplicationConfiguration application, Type task)
            : base(application, task)
        {
        }

        public TaskConfiguration<TWorkItem> Step<TStep>()
            where TStep : IStep<TWorkItem>
        {
            Steps.Add(typeof (TStep));

            return this;
        }

        public TaskConfiguration<TWorkItem> Clear()
        {
            Steps.Clear();

            return this;
        }

        public TaskConfiguration<TWorkItem> Remove<TStep>()
            where TStep : IStep<TWorkItem>
        {
            Steps.RemoveAll(x => x == typeof (TStep));

            return this;
        }

        internal override IWindsorInstaller GetInstaller()
        {
            return new TaskInstaller<TWorkItem>(Task, Steps);
        }
    }
}