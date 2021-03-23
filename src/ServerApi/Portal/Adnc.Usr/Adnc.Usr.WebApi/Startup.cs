using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Authorization;
using Autofac;
using Adnc.Infr.Consul;
using Adnc.Usr.Application;
using Adnc.WebApi.Shared;

namespace Adnc.Usr.WebApi
{
    public class Startup
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly ServiceInfo _serviceInfo;

        public Startup(IConfiguration configuration
            , IWebHostEnvironment environment)
        {
            _configuration = configuration;
            _environment = environment;
            _serviceInfo = ServiceInfo.Create(Assembly.GetExecutingAssembly());
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddAdncServices<PermissionHandlerLocal>(_configuration, _environment, _serviceInfo);
        }

        public void ConfigureContainer(ContainerBuilder builder)
        {
            builder.RegisterAdncModules<AdncUsrApplicationModule>(_configuration);
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseAdncMiddlewares(_configuration, _environment, _serviceInfo);

            if (_environment.IsProduction() || _environment.IsStaging())
                app.RegisterToConsul();
        }
    }
}