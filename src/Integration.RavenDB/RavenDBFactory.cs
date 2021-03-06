﻿using System;
using Castle.MicroKernel;
using Raven.Client;
using Vertica.Integration.RavenDB.Infrastructure;

namespace Vertica.Integration.RavenDB
{
	internal class RavenDbFactory<TConnection> : IRavenDbFactory<TConnection>, IDisposable
		where TConnection : Connection
	{
		private readonly Lazy<IDocumentStore> _documentStore;

		public RavenDbFactory(TConnection connection, IKernel kernel)
		{
			if (connection == null) throw new ArgumentNullException(nameof(connection));
			
			_documentStore = new Lazy<IDocumentStore>(() =>
			{
				IDocumentStore documentStore = connection.Create(kernel);
				connection.Initialize(documentStore, kernel);

				return documentStore;
			});
		}

		public IDocumentStore DocumentStore => _documentStore.Value;

		public void Dispose()
		{
			if (_documentStore.IsValueCreated)
				_documentStore.Value.Dispose();
		}
	}

	internal class RavenDbFactory : IRavenDbFactory
	{
		private readonly IRavenDbFactory<DefaultConnection> _decoree;

		public RavenDbFactory(IRavenDbFactory<DefaultConnection> decoree)
		{
			if (decoree == null) throw new ArgumentNullException(nameof(decoree));

			_decoree = decoree;
		}

		public IDocumentStore DocumentStore => _decoree.DocumentStore;

		public void Dispose()
		{
			_decoree.Dispose();
		}
	}
}