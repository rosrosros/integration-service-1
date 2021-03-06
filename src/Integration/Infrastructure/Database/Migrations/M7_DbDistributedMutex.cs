﻿using System;
using FluentMigrator;
using Vertica.Integration.Infrastructure.Database.Migrations.Features;

namespace Vertica.Integration.Infrastructure.Database.Migrations
{
    [Migration(7)]
    [DbDistributedMutexFeature]
    public class M7_DbDistributedMutex : IntegrationMigration
    {
        public override void Up()
        {
            var configuration = Resolve<IIntegrationDatabaseConfiguration>();

            Create.Table(configuration.TableName(IntegrationDbTable.DistributedMutex))
                .WithColumn("Name").AsString(50).PrimaryKey()
                .WithColumn("LockId").AsGuid()
                .WithColumn("CreatedAt").AsDateTimeOffset()
                .WithColumn("MachineName").AsString(50).Nullable();
        }

        public override void Down()
        {
            throw new NotSupportedException("Migrating down is not supported.");
        }
    }
}