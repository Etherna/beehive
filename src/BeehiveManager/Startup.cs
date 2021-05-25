//   Copyright 2021-present Etherna Sagl
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.

using Etherna.BeehiveManager.Configs;
using Etherna.BeehiveManager.Configs.Hangfire;
using Etherna.BeehiveManager.Configs.Swagger;
using Etherna.BeehiveManager.Domain;
using Etherna.BeehiveManager.Domain.Models;
using Etherna.BeehiveManager.Extensions;
using Etherna.BeehiveManager.Persistence;
using Etherna.BeehiveManager.Services.Tasks;
using Etherna.MongODM.Core.Options;
using Hangfire;
using Hangfire.Mongo;
using Hangfire.Mongo.Migration.Strategies;
using Hangfire.Mongo.Migration.Strategies.Backup;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.IO;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Etherna.BeehiveManager
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
            // Configure Asp.Net Core framework services.
            services.AddDataProtection()
                .PersistKeysToDbContext(new DbContextOptions { ConnectionString = Configuration["ConnectionStrings:SystemDb"] });

            services.AddCors();
            services.AddControllers()
                .AddJsonOptions(options =>
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
            services.AddApiVersioning(options =>
            {
                options.ReportApiVersions = true;
            });
            services.AddVersionedApiExplorer(options =>
            {
                // add the versioned api explorer, which also adds IApiVersionDescriptionProvider service
                // note: the specified format code will format the version as "'v'major[.minor][-status]"
                options.GroupNameFormat = "'v'VVV";

                // note: this option is only necessary when versioning by url segment. the SubstitutionFormat
                // can also be used to control the format of the API version in route templates
                options.SubstituteApiVersionInUrl = true;
            });

            // Configure Swagger services.
            services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
            services.AddSwaggerGen(options =>
            {
                //add a custom operation filter which sets default values
                options.OperationFilter<SwaggerDefaultValues>();

                //integrate xml comments
                var xmlFile = typeof(Startup).GetTypeInfo().Assembly.GetName().Name + ".xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                options.IncludeXmlComments(xmlPath);
            });

            // Configure setting.
            var assemblyVersion = new AssemblyVersion(GetType().GetTypeInfo().Assembly);
            services.Configure<ApplicationSettings>(options =>
            {
                options.AssemblyVersion = assemblyVersion.Version;
            });

            // Configure Hangfire and persistence.
            services.AddMongODMWithHangfire<ModelBase>(configureHangfireOptions: options =>
            {
                options.ConnectionString = Configuration["ConnectionStrings:HangfireDb"];
                options.StorageOptions = new MongoStorageOptions
                {
                    MigrationOptions = new MongoMigrationOptions //don't remove, could throw exception
                    {
                        MigrationStrategy = new MigrateMongoMigrationStrategy(),
                        BackupStrategy = new CollectionMongoBackupStrategy()
                    }
                };
            })
                .AddDbContext<IBeehiveContext, BeehiveContext>(options =>
                {
                    options.DocumentSemVer.CurrentVersion = assemblyVersion.SimpleVersion;
                    options.ConnectionString = Configuration["ConnectionStrings:BeehiveManagerDb"];
                });

            // Configure domain services.
            services.AddDomainServices();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IApiVersionDescriptionProvider apiProvider)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseAuthorization();

            // Add Hangfire.
            app.UseHangfireDashboard(
                "/admin/hangfire",
                new DashboardOptions { Authorization = new[] { new AllowAllFilter() } });

            if (!env.IsStaging()) //don't init server in staging
            {
                //register hangfire server
                app.UseHangfireServer(new BackgroundJobServerOptions
                {
                    Queues = new[]
                    {
                        Services.Tasks.Queues.DOMAIN_MAINTENANCE,
                        "default"
                    }
                });

                //register cron tasks
                RecurringJob.AddOrUpdate<IRefreshAllNodesStatusTask>(
                    RefreshAllNodesStatusTask.TaskId,
                    task => task.RunAsync(),
                    "*/15 * * * *"); //every 15 minutes

                RecurringJob.AddOrUpdate<ICashoutAllNodesTask>(
                    CashoutAllNodesTask.TaskId,
                    task => task.RunAsync(),
                    "0 5 * * *"); //at 05:00 every day
            }

            // Add Swagger and SwaggerUI.
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                // build a swagger endpoint for each discovered API version
                foreach (var description in apiProvider.ApiVersionDescriptions)
                {
                    options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", description.GroupName.ToUpperInvariant());
                }
            });

            // Add controllers.
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
