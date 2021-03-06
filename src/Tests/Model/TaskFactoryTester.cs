﻿using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Vertica.Integration.Infrastructure.Extensions;
using Vertica.Integration.Model;
using Vertica.Integration.Model.Exceptions;
using Vertica.Integration.Tests.Infrastructure.Testing;
using Vertica.Integration.Tests.SQLite;

namespace Vertica.Integration.Tests.Model
{
    [TestFixture(Category = "Integration")]
    public class TaskFactoryTester
    {
        [Test]
        public void Exists_And_Is_ITask()
        {
            ITaskFactory subject;
            using (CreateSubject(out subject, tasks => AddFromTestAssembly(tasks)))
            {
                Assert.IsTrue(subject.Exists("TestTask"));
            }            
        }

        [Test]
        public void Get_ByName__Verify_SingletonBehaviour()
        {
            ITaskFactory subject;
            using (CreateSubject(out subject, tasks => AddFromTestAssembly(tasks)))
            {
                ITask task1 = subject.Get("TestTask");
                ITask task2 = subject.Get("TestTask");

                Assert.That(task1, Is.EqualTo(task2));
            }
        }

        [Test]
        public void Get_ByName__Verify_NameIsNotCaseSensitive()
        {
            ITaskFactory subject;
            using (CreateSubject(out subject, tasks => AddFromTestAssembly(tasks)))
            {
                ITask task1 = subject.Get("testtask");

                Assert.That(task1, Is.Not.Null);
                Assert.That(task1, Is.TypeOf<TestTask>());
            }
        }

        [Test]
        public void Add_Specific_Task()
        {
            ITaskFactory subject;
            using (CreateSubject(out subject, tasks => tasks.Clear().Task<TestTask>()))
            {
                Assert.That(subject.Get<TestTask>(), Is.Not.Null);

                ITask[] tasks = subject.GetAll();
                Assert.That(tasks.Length, Is.EqualTo(1));
            }
        }

        [Test]
        public void Scan_Tasks_Add_Ignore()
        {
            ITaskFactory subject;
            using (CreateSubject(out subject, tasks => AddFromTestAssembly(tasks).Remove<TestTask>().Task<AnotherTask>()))
            {
                TaskNotFoundException ex = Assert.Throws<TaskNotFoundException>(() => subject.Get<TestTask>());
                Assert.That(ex.Message, Does.Contain("TestTask"));

                ITask anotherTask = subject.Get<AnotherTask>();
                Assert.That(anotherTask, Is.Not.Null);

                ITask[] tasks = subject.GetAll();
                Assert.That(tasks.Count(x => x.Equals(anotherTask)), Is.EqualTo(1));
            }
        }

        [Test]
        public void Scan_Tasks_Add_Ignore_Clear()
        {
            ITaskFactory subject;
            using (CreateSubject(out subject, tasks => AddFromTestAssembly(tasks).Remove<TestTask>().Task<AnotherTask>().Clear()))
            {
                Assert.That(subject.GetAll().Length, Is.EqualTo(0));
            }
        }

        [Test]
        public void Scan_Add_TaskWithSteps()
        {
            ITaskFactory subject;
            using (CreateSubject(out subject, tasks => AddFromTestAssembly(tasks)
                .Task<TaskWithStepsTask, TaskWithStepsWorkItem>(task => task
                    .Step<Step1>()
                    .Step<Step2>())))
            {
                ITask task = subject.Get<TaskWithStepsTask>();
                Assert.That(task, Is.Not.Null);
            }
        }

        [Test]
        public void GetAll_No_Duplicates()
        {
            ITaskFactory subject;
            using (CreateSubject(out subject, tasks => AddFromTestAssembly(tasks).Task<TestTask>()))
            {
                string[] tasks = subject.GetAll().Select(x => x.Name()).ToArray();

                bool duplicates = tasks
                    .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .Any(x => x.Count() > 1);

                Assert.That(duplicates, Is.False,
	                $"One or more tasks are duplicated: {string.Join(", ", tasks)}.");
            }
        }

        private static TasksConfiguration AddFromTestAssembly(TasksConfiguration tasks)
        {
            return tasks
				.Clear()
				.AddFromAssemblyOfThis<TaskFactoryTester>()
				.Remove<SQLiteTester.TestMigrationsTask>()
                .Remove<TaskDistributedMutexTester.SomeTask>();
        }

        private static IDisposable CreateSubject(out ITaskFactory subject, Action<TasksConfiguration> tasks)
        {
            var context = ApplicationContext.Create(application => application
                .ConfigureForUnitTest()
                .Tasks(tasks));

            subject = context.Resolve<ITaskFactory>();

            return context;
        }

        public class TestTask : IntegrationTask, IEquatable<TestTask>
        {
            private readonly Guid _id;

            public TestTask()
            {
                _id = Guid.NewGuid();
            }

            public override string Description => $"TestTask-{_id:D}";

	        public override string ToString()
            {
                return Description;
            }

            public override void StartTask(ITaskExecutionContext context)
            {
                throw new NotSupportedException();
            }

            public bool Equals(TestTask other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return _id.Equals(other._id);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((TestTask)obj);
            }

            public override int GetHashCode()
            {
                return _id.GetHashCode();
            }
        }

        public class AnotherTask : IntegrationTask
        {
            public override void StartTask(ITaskExecutionContext context)
            {
            }

            public override string Description => string.Empty;
        }

        public class TaskWithStepsTask : IntegrationTask<TaskWithStepsWorkItem>
        {
            public TaskWithStepsTask(IEnumerable<IStep<TaskWithStepsWorkItem>> steps) : base(steps)
            {
            }

            public override string Description => string.Empty;

	        public override TaskWithStepsWorkItem Start(ITaskExecutionContext context)
            {
                return new TaskWithStepsWorkItem();
            }
        }

        public class Step1 : Step<TaskWithStepsWorkItem>
        {
            public override string Description => string.Empty;

	        public override void Execute(ITaskExecutionContext<TaskWithStepsWorkItem> context)
            {
            }
        }

        public class Step2 : Step<TaskWithStepsWorkItem>
        {
            public override string Description => string.Empty;

	        public override void Execute(ITaskExecutionContext<TaskWithStepsWorkItem> context)
            {
            }
        }

        public class TaskWithStepsWorkItem
        {
        }
    }

}