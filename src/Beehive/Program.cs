// Copyright 2021-present Etherna SA
// This file is part of Beehive.
// 
// Beehive is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// 
// Beehive is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License along with Beehive.
// If not, see <https://www.gnu.org/licenses/>.

using Etherna.ACR.Middlewares.DebugPages;
using Etherna.Beehive.Configs;
using Etherna.Beehive.Configs.MongODM;
using Etherna.Beehive.Configs.Swagger;
using Etherna.Beehive.Domain;
using Etherna.Beehive.Domain.Models;
using Etherna.Beehive.Exceptions;
using Etherna.Beehive.Extensions;
using Etherna.Beehive.Persistence;
using Etherna.Beehive.Services;
using Etherna.Beehive.Services.Settings;
using Etherna.Beehive.Services.Tasks;
using Etherna.Beehive.Settings;
using Etherna.DomainEvents;
using Etherna.MongODM;
using Etherna.MongODM.AspNetCore.UI;
using Etherna.MongODM.Core.Options;
using Hangfire;
using Hangfire.Mongo;
using Hangfire.Mongo.Migration.Strategies;
using Hangfire.Mongo.Migration.Strategies.Backup;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Exceptions;
using Serilog.Sinks.Elasticsearch;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using DashboardOptions = Etherna.MongODM.AspNetCore.UI.DashboardOptions;

namespace Etherna.Beehive
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            // Configure logging first.
            ConfigureLogging();

            // Then create the host, so that if the host fails we can log errors.
            try
            {
                Log.Information("Starting web host");

                var builder = WebApplication.CreateBuilder(args);

                // Configs.
                builder.Host.UseSerilog();

                ConfigureServices(builder);

                var app = builder.Build();
                ConfigureApplication(app);

                // First operations.
                app.SeedDbContexts();
                app.StartBeeNodeLiveManager();

                // Run application.
                app.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
                throw;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        // Helpers.
        private static void ConfigureLogging()
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? throw new ServiceConfigurationException();
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{environment}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .Enrich.WithExceptionDetails()
                .Enrich.WithMachineName()
                .WriteTo.Debug(formatProvider: CultureInfo.InvariantCulture)
                .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
                .WriteTo.Elasticsearch(ConfigureElasticSink(configuration, environment))
                .Enrich.WithProperty("Environment", environment)
                .ReadFrom.Configuration(configuration)
                .CreateLogger();
        }

        private static ElasticsearchSinkOptions ConfigureElasticSink(IConfigurationRoot configuration, string environment)
        {
            string assemblyName = Assembly.GetExecutingAssembly().GetName().Name!.ToLower(CultureInfo.InvariantCulture).Replace(".", "-", StringComparison.InvariantCulture);
            string envName = environment.ToLower(CultureInfo.InvariantCulture).Replace(".", "-", StringComparison.InvariantCulture);
            return new ElasticsearchSinkOptions((configuration.GetSection("Elastic:Urls").Get<string[]>() ?? throw new ServiceConfigurationException()).Select(u => new Uri(u)))
            {
                AutoRegisterTemplate = true,
                IndexFormat = $"{assemblyName}-{envName}-{DateTime.UtcNow:yyyy-MM}"
            };
        }

        private static void ConfigureServices(WebApplicationBuilder builder)
        {
            var services = builder.Services;
            var config = builder.Configuration;
            var env = builder.Environment;

            // Configure Asp.Net Core framework services.
            services.AddDataProtection()
                .PersistKeysToDbContext(new DbContextOptions { ConnectionString = config["ConnectionStrings:DataProtectionDb"] ?? throw new ServiceConfigurationException() });

            services.AddCors();
            services.AddRazorPages();
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

            // Configure Hangfire server.
            if (!env.IsStaging()) //don't start server in staging
            {
                //register hangfire server
                services.AddHangfireServer(options =>
                {
                    options.Queues = new[]
                    {
                        Queues.DOMAIN_MAINTENANCE,
                        Queues.PIN_CONTENTS,
                        Queues.NODE_MAINTENANCE,
                        "default"
                    };
                    options.WorkerCount = Environment.ProcessorCount * 2;
                });
            }

            // Configure Swagger services.
            services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
            services.AddSwaggerGen(options =>
            {
                options.SupportNonNullableReferenceTypes();
                options.UseAllOfToExtendReferenceSchemas();
                options.UseInlineDefinitionsForEnums();

                //add a custom operation filter which sets default values
                options.OperationFilter<SwaggerDefaultValues>();

                //integrate xml comments
                var xmlFile = typeof(Program).GetTypeInfo().Assembly.GetName().Name + ".xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                options.IncludeXmlComments(xmlPath);
            });

            // Configure setting.
            services.Configure<CashoutAllNodesChequesSettings>(config.GetSection(CashoutAllNodesChequesSettings.ConfigPosition));
            services.Configure<NodesAddressMaintainerSettings>(config.GetSection(NodesAddressMaintainerSettings.ConfigPosition));
            services.Configure<NodesChequebookMaintainerSettings>(config.GetSection(NodesChequebookMaintainerSettings.ConfigPosition));
            services.Configure<SeedDbSettings>(config.GetSection(SeedDbSettings.ConfigPosition));

            // Configure Hangfire and persistence.
            services.AddMongODMWithHangfire(configureHangfireOptions: options =>
            {
                options.ConnectionString = config["ConnectionStrings:HangfireDb"] ?? throw new ServiceConfigurationException();
                options.StorageOptions = new MongoStorageOptions
                {
                    MigrationOptions = new MongoMigrationOptions //don't remove, could throw exception
                    {
                        MigrationStrategy = new MigrateMongoMigrationStrategy(),
                        BackupStrategy = new CollectionMongoBackupStrategy()
                    }
                };
            })
                .AddDbContext<IBeehiveDbContext, BeehiveDbContext>(sp =>
                {
                    var eventDispatcher = sp.GetRequiredService<IEventDispatcher>();
                    var seedDbSettings = sp.GetRequiredService<IOptions<SeedDbSettings>>().Value;
                    return new BeehiveDbContext(
                        eventDispatcher,
                        Enumerable.Where(seedDbSettings.BeeNodes, n => n is not null)
                                               .Select(n => new BeeNode(n.Scheme, n.GatewayPort, n.Hostname, n.EnableBatchCreation)));
                },
                options =>
                {
                    options.ConnectionString = config["ConnectionStrings:BeehiveDb"] ?? throw new ServiceConfigurationException();
                });

            services.AddMongODMAdminDashboard(new DashboardOptions
            {
                AuthFilters = new[] { new AllowAllFilter() },
                BasePath = CommonConsts.DatabaseAdminPath
            });

            // Configure domain services.
            services.AddDomainServices();
        }

        private static void ConfigureApplication(WebApplication app)
        {
            var env = app.Environment;

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseEthernaAcrDebugPages();
            }

            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthorization();

            // Add Hangfire.
            app.UseHangfireDashboard(
                CommonConsts.HangfireAdminPath,
                new Hangfire.DashboardOptions { Authorization = new[] { new Configs.Hangfire.AllowAllFilter() } });

            // Add Swagger and SwaggerUI.
            var apiProvider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.DocumentTitle = "Beehive API";

                // build a swagger endpoint for each discovered API version
                foreach (var description in apiProvider.ApiVersionDescriptions)
                {
                    options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", description.GroupName.ToUpperInvariant());
                }
            });

            // Add pages and controllers.
            app.MapControllers();
            app.MapRazorPages();

            // Register cron tasks.
            RecurringJob.AddOrUpdate<ICashoutAllNodesChequesTask>(
                CashoutAllNodesChequesTask.TaskId,
                task => task.RunAsync(),
                Cron.Daily(5));

            RecurringJob.AddOrUpdate<INodesAddressMaintainerTask>(
                NodesAddressMaintainerTask.TaskId,
                task => task.RunAsync(),
                Cron.Hourly(5));

            RecurringJob.AddOrUpdate<INodesChequebookMaintainerTask>(
                NodesChequebookMaintainerTask.TaskId,
                task => task.RunAsync(),
                Cron.Daily(5, 15));
        }
    }
}
