using Microsoft.Extensions.DependencyInjection;
using Microsoft.Owin;
using Owin;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Mvc;

[assembly: OwinStartup(typeof(Sample.NetFramework.Postgres.Startup))]

namespace Sample.NetFramework.Postgres
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            // configure CAP
            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            ConfigureServices(services);
            var dependencyResolver = ConfigureDI(services);
            StartCAP(dependencyResolver);
        }

        private void ConfigureServices(ServiceCollection services)
        {
            services.AddCap(x =>
            {
                x.TopicNamePrefix = "Sample";
                x.GroupNamePrefix = "Sample";
                x.UseAmazonSQS(options =>
                {
                    options.Region = Amazon.RegionEndpoint.APSoutheast2;
                });
                x.UsePostgreSql(options =>
                {
                    options.ConnectionString = "host=spotlight-local;database=CAP;password=wQZkW6VribhtKtN3;username=postgres";
                    options.Schema = "Sample";
                });
                x.ConsumingAssembly = Assembly.GetExecutingAssembly();      // if you don't set this, StartIfNotAlreadyRunning doesn't know which assembly to search for CAPSubscribers, and so doesn't find them
            });

            // note, if you're using a logger other than Microsoft.Extensions.Logging.ILogger, you still need this
            // or else CAP will fall over when it tries to log
            services.AddLogging();

            // register controllers
            var controllerTypes = Assembly.GetExecutingAssembly().GetExportedTypes()
                .Where(t => !t.IsAbstract && !t.IsGenericTypeDefinition)
                .Where(t => typeof(IController).IsAssignableFrom(t) || t.Name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase));

            foreach (var controllerType in controllerTypes)
                services.AddTransient(controllerType);
        }

        private IDependencyResolver ConfigureDI(ServiceCollection services)
        {
            // note, if you're using DI other than Microsoft.Extensions, just populate services into it (to get all the CAP DI registrations) instead of this

            // this is how to do it for an MVC project - would be different for a WebAPI or Console App project
            var dependencyResolver = new DefaultDependencyResolver(services.BuildServiceProvider());
            DependencyResolver.SetResolver(dependencyResolver);
            return dependencyResolver;
        }

        private void StartCAP(IDependencyResolver dependencyResolver)
        {
            // and start CAP running (normally, this is done automatically by CAP as a IHost background job,
            // but .NET Framework doesn't run the infrastructure for this, so you need to do it manually)

            // (or do this using Hangfire.IO, etc, but note it MUST be running from the same DI container as 
            // we have to start, run all publishing\subscribing, and dispose of the same singleton instance of Bootstrapper)            
            var capBootstrapper = dependencyResolver.GetService<DotNetCore.CAP.Internal.Bootstrapper>();
            capBootstrapper.StartIfNotAlreadyRunning();

            // store the bootstrapper in state so we can dispose of it properly in Global.asax on application shutdown
            var state = System.Web.HttpContext.Current?.Application;
            if (state != null)
            {
                state.Lock();
                state["CAPBootstrapper"] = capBootstrapper;
                state.UnLock();
            }
        }
    }
}
