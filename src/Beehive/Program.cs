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

using Elastic.Ingest.Elasticsearch;
using Elastic.Ingest.Elasticsearch.DataStreams;
using Elastic.Serilog.Sinks;
using Etherna.ACR.Middlewares.DebugPages;
using Etherna.Beehive.Areas.Api;
using Etherna.Beehive.Areas.Api.SwarmApiHandlers;
using Etherna.Beehive.Configs;
using Etherna.Beehive.Configs.MongODM;
using Etherna.Beehive.Configs.OpenApi;
using Etherna.Beehive.Domain;
using Etherna.Beehive.Domain.Models;
using Etherna.Beehive.Exceptions;
using Etherna.Beehive.Extensions;
using Etherna.Beehive.Options;
using Etherna.Beehive.Persistence;
using Etherna.Beehive.Services;
using Etherna.Beehive.Services.Options;
using Etherna.Beehive.Services.Tasks;
using Etherna.Beehive.Services.Tasks.Background;
using Etherna.Beehive.Services.Tasks.Cron;
using Etherna.BeeNet.Services;
using Etherna.DomainEvents;
using Etherna.MongODM;
using Etherna.MongODM.AspNetCore.UI;
using Hangfire;
using Hangfire.Mongo;
using Hangfire.Mongo.Migration.Strategies;
using Hangfire.Mongo.Migration.Strategies.Backup;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Exceptions;
using System;
using System.Globalization;
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
                builder.WebHost.ConfigureKestrel(serverOptions =>
                {
                    serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(10);
                    serverOptions.Limits.MaxRequestBodySize = null;
                });

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

            var elasticNodes = (configuration.GetSection("Elastic:Urls").Get<string[]>() ?? throw new ServiceConfigurationException())
                .Select(u => new Uri(u))
                .ToArray();
            var assemblyName = Assembly.GetExecutingAssembly().GetName().Name!.ToLower(CultureInfo.InvariantCulture).Replace(".", "-", StringComparison.InvariantCulture);
            var envName = environment.ToLower(CultureInfo.InvariantCulture).Replace(".", "-", StringComparison.InvariantCulture);

            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .Enrich.WithExceptionDetails()
                .Enrich.WithMachineName()
                .WriteTo.Debug(formatProvider: CultureInfo.InvariantCulture)
                .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
                .WriteTo.Elasticsearch(elasticNodes, opts =>
                {
                    opts.BootstrapMethod = BootstrapMethod.Silent;
                    opts.DataStream = new DataStreamName("logs", assemblyName, envName);
                })
                .Enrich.WithProperty("Environment", environment)
                .ReadFrom.Configuration(configuration)
                .CreateLogger();
        }

        private static void ConfigureServices(WebApplicationBuilder builder)
        {
            var config = builder.Configuration;
            var env = builder.Environment;
            var services = builder.Services;

            // Configure Asp.Net Core framework services.
            services.AddCors();
            services.AddOpenApi("beehive04", options =>
            {
                options.AddDocumentTransformer(new BeehiveDocumentTransformer());
                options.AddDocumentTransformer<MetadataFilterDocumentTransformer<BeehiveApiMarker>>();

                options.AddOperationTransformer<DeprecatedOperationTransformer>();
                options.AddOperationTransformer<RemoveDefaultResponse200OperationTransformer>();
                options.AddOperationTransformer<BeehiveOperationTransformer>();
                
                options.AddSchemaTransformer<SwarmModelsSchemaTransformer>();
            });
            services.AddOpenApi("swarm", options =>
            {
                options.AddDocumentTransformer(new SwarmDocumentTransformer());
                options.AddDocumentTransformer<MetadataFilterDocumentTransformer<SwarmApiMarker>>();

                options.AddOperationTransformer<AcceptsUnrestrictedOperationTransformer>();
                options.AddOperationTransformer<DeprecatedOperationTransformer>();
                options.AddOperationTransformer<RemoveDefaultResponse200OperationTransformer>();
                options.AddOperationTransformer<SwarmOperationTransformer>();

                options.AddSchemaTransformer<SwarmModelsSchemaTransformer>();
            });
            services.AddOpenApi("swarmv1", options =>
            {
                options.AddDocumentTransformer(new SwarmDocumentTransformer());
                options.AddDocumentTransformer<MetadataFilterDocumentTransformer<SwarmV1ApiMarker>>();

                options.AddOperationTransformer<AcceptsUnrestrictedOperationTransformer>();
                options.AddOperationTransformer<DeprecatedOperationTransformer>();
                options.AddOperationTransformer<RemoveDefaultResponse200OperationTransformer>();
                options.AddOperationTransformer<SwarmOperationTransformer>();

                options.AddSchemaTransformer<SwarmModelsSchemaTransformer>();
            });
            services.AddRazorPages();
            services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

            // Configure Hangfire server.
            if (!env.IsStaging()) //don't start server in staging
            {
                services.AddHangfireServer(options =>
                {
                    options.Queues =
                    [
                        Queues.DOMAIN_MAINTENANCE,
                        Queues.DB_MAINTENANCE,
                        Queues.PIN_CONTENTS,
                        Queues.NODE_MAINTENANCE,
                        "default"
                    ];
                });
            }
            
            // Configure Bee.Net
            services.AddScoped<IChunkService, ChunkService>();
            services.AddScoped<IFeedService, FeedService>();

            // Configure options.
            services.Configure<CashoutAllNodesChequesOptions>(config.GetSection(CashoutAllNodesChequesOptions.ConfigPosition));
            services.Configure<NodesAddressMaintainerOptions>(config.GetSection(NodesAddressMaintainerOptions.ConfigPosition));
            services.Configure<NodesChequebookMaintainerOptions>(config.GetSection(NodesChequebookMaintainerOptions.ConfigPosition));
            services.Configure<SeedDbOptions>(config.GetSection(SeedDbOptions.ConfigPosition));
            
            // Configure api handler.
            //beehive
            services.AddScoped<IBeehiveApiHandler, BeehiveApiHandler>();
            //swarm
            services.AddScoped<IBatchesApiHandler, BatchesApiHandler>();
            services.AddScoped<IBytesApiHandler, BytesApiHandler>();
            services.AddScoped<IBzzApiHandler, BzzApiHandler>();
            services.AddScoped<IChainstateApiHandler, ChainstateApiHandler>();
            services.AddScoped<IChunksApiHandler, ChunksApiHandler>();
            services.AddScoped<IFeedsApiHandler, FeedsApiHandler>();
            services.AddScoped<IHealthApiHandler, HealthApiHandler>();
            services.AddScoped<INodeApiHandler, NodeApiHandler>();
            services.AddScoped<IPinsApiHandler, PinsApiHandler>();
            services.AddScoped<IReadinessApiHandler, ReadinessApiHandler>();
            services.AddScoped<ISocApiHandler, SocApiHandler>();
            services.AddScoped<IStampsApiHandler, StampsApiHandler>();

            // Configure Hangfire and persistence.
            services.AddMongODMWithHangfire(configureHangfireOptions: options =>
            {
                options.ConnectionString = config["ConnectionStrings:HangfireDb"] ??
                                           throw new ServiceConfigurationException("Hangfire connection string is not defined");
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
                    var seedDbSettings = sp.GetRequiredService<IOptions<SeedDbOptions>>().Value;
                    return new BeehiveDbContext(
                        eventDispatcher,
                        seedDbSettings.BeeNodes
                            .Select(n => new BeeNode(new Uri(n.ConnectionString, UriKind.Absolute), n.EnableBatchCreation))
                            .ToArray());
                },
                options =>
                {
                    options.ConnectionString = config["ConnectionStrings:BeehiveDb"] ??
                                               throw new ServiceConfigurationException("BeehiveDb connection string is not defined");
                });

            services.AddMongODMAdminDashboard(new DashboardOptions
            {
                AuthFilters = [new AllowAllFilter()],
                BasePath = CommonConsts.DatabaseAdminPath
            });

            // Configure domain services.
            services.AddDomainServices();
            
            // Configure background services.
            //push chunks
            if (!bool.TryParse(config["PushChunks:Enabled"], out var enablePushChunks))
                enablePushChunks = true;
            if (enablePushChunks)
                services.AddHostedService<PushChunksBackgroundService>();
        }

        private static void ConfigureApplication(WebApplication app)
        {
            var env = app.Environment;

            app.UseCors(builder =>
            {
                if (env.IsDevelopment())
                {
                    builder.SetIsOriginAllowed(_ => true)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                }
                else
                {
                    builder.WithOrigins("https://etherna.io")
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                }
            });

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseEthernaAcrDebugPages();
            }

            app.UseStaticFiles();
            app.UseRouting();
            
            app.UseAuthorization();

            // Add api and pages.
            app.MapOpenApi();
            app.MapRazorPages();
            
            app.MapBeehiveApi();
            app.MapSwarmApi();

            // Add Hangfire.
            app.UseHangfireDashboard(
                CommonConsts.HangfireAdminPath,
                new Hangfire.DashboardOptions
                {
                    Authorization = [new Configs.Hangfire.AllowAllFilter()],
                    IgnoreAntiforgeryToken = true
                });

            // Add SwaggerUI.
            app.UseSwaggerUI(options =>
            {
                options.DocumentTitle = "Beehive API";

                // build a swagger endpoint for each discovered API version
                options.SwaggerEndpoint("/openapi/swarm.json", "Swarm API");
                options.SwaggerEndpoint("/openapi/swarmv1.json", "Swarm V1 API");
                options.SwaggerEndpoint("/openapi/beehive04.json", "Beehive v0.4 API");
            });

            // Register cron tasks.
            RecurringJob.AddOrUpdate<ICashoutAllNodesChequesTask>(
                CashoutAllNodesChequesTask.TaskId,
                task => task.RunAsync(),
                Cron.Daily(5));
            
            RecurringJob.AddOrUpdate<ICleanupExpiredLocksTask>(
                CleanupExpiredLocksTask.TaskId,
                task => task.RunAsync(),
                Cron.Daily(3));
            
            RecurringJob.AddOrUpdate<ICleanupOldFailedTasksTask>(
                CleanupOldFailedTasksTask.TaskId,
                task => task.RunAsync(),
                Cron.Daily);

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
