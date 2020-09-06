using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DemonFox.Tails.Core.Entities.Generator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DemonFox.Tails.AirGo
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            services.AddMvc(options =>
            {
                options.EnableEndpointRouting = false;
            });
                         
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                //app.UseExceptionHandler("/Home/Error");
                app.UseExceptionHandler(new ExceptionHandlerOptions
                {
                    ExceptionHandler = async httpContext =>
                    {
                        // ExceptionHandlerOptions的"异常处理器"属性                    
                        httpContext.Response.ContentType = "application/json";
                        httpContext.Response.Clear();
                        Exception ex = httpContext.Features.Get<IExceptionHandlerPathFeature>().Error;
                        await httpContext.Response.WriteAsync($@"{{""code"": 0, ""msg"": ""{ex.Message}""}}");
                    }
                });
            }



            app.UseStaticFiles();
            app.UseCookiePolicy();

            //app.UseMvc(routes =>
            //{
            //    routes.MapRoute(
            //        name: "default",
            //        template: "{controller=Home}/{action=Prepare}/{id?}");
            //});
            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller}/{action}/{id?}");
            });            
        }
    }
}
