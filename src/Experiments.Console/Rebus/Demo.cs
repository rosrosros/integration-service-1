﻿using Rebus.Routing.TypeBased;
using Rebus.Transport.InMem;
using Vertica.Integration;
using Vertica.Integration.Domain.LiteServer;
using Vertica.Integration.Infrastructure;
using Vertica.Integration.Rebus;

namespace Experiments.Console.Rebus
{
    public static class Demo
    {
        public static void Run()
        {
            using (var context = ApplicationContext.Create(application => application
                .Database(database => database
                    .IntegrationDb(integrationDb => integrationDb
                        .PrefixTables("IntegrationDb_")
                        .Connection(ConnectionString.FromText(@"Integrated Security=SSPI;Data Source=.\SQLExpress;Database=IntegrationService_Rebus"))))
                .UseRebus(rebus => rebus
                    .Bus((bus, kernel) => bus
                        .Logging(logging => logging
                            .None())
                            //.ColoredConsole(LogLevel.Error))
                            //.Use(kernel.Resolve<RebusLoggerFactory>()))
                        .Routing(routing => routing
                            .TypeBased()
                            .Map<string>("inputQueue")
                            .Map<long>("inputQueue"))
                        //.Subscriptions(subscriptions => subscriptions
                        //    .UseJsonFile(@"c:\tmp\rebus\json"))
                        .Transport(transport => transport
                            .UseInMemoryTransport(new InMemNetwork(true), "inputQueue")
                            //.UseSqlServer(kernel.Resolve<IDbFactory>().GetConnection().ConnectionString, "Rebus", "inputQueue")
                            //.UseFileSystem(@"c:\tmp\rebus", "inputQueue")
                            //.UseAzureServiceBus("Endpoint=sb://XXXXXX.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=XXXXXXX", "inputQueue")
                        )
                    )
                    .Handlers(handlers => handlers
                        .Handler<ReceiveMessageHandler>()
                        .Handler<ReceiveAnotherMessageHandler>()))
                .UseLiteServer(liteServer => liteServer
                    .OnStartup(startup => startup
                        .RunMigrateTask())
                    .AddRebus()
                    .AddServer<SendMessageServer>()
                    .AddWorker<SendMessageWorker>())
                .Services(services => services
                    .Advanced(advanced => advanced
                        .Register<RebusLoggerFactory>()))))
            {
                context.Execute(nameof(LiteServerHost));
            }
        }
    }
}