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

using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Etherna.ACR.Middlewares.DebugPages;
using Etherna.Beehive.Configs;
using Etherna.Beehive.Configs.MongODM;
using Etherna.Beehive.Configs.Swagger;
using Etherna.Beehive.Configs.Swagger.OperationFilters;
using Etherna.Beehive.Configs.Swagger.SchemaFilters;
using Etherna.Beehive.Converters;
using Etherna.Beehive.Domain;
using Etherna.Beehive.Domain.Models;
using Etherna.Beehive.Exceptions;
using Etherna.Beehive.Extensions;
using Etherna.Beehive.Options;
using Etherna.Beehive.Persistence;
using Etherna.Beehive.Services;
using Etherna.Beehive.Services.Chunks;
using Etherna.Beehive.Services.Options;
using Etherna.Beehive.Services.Tasks;
using Etherna.BeeNet.Models;
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
using Serilog.Sinks.Elasticsearch;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.ComponentModel;
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

        private static void ConfigureServices(WebApplicationBuilder builder)
        {
            var config = builder.Configuration;
            var env = builder.Environment;
            var services = builder.Services;
            
            // Register global TypeConverters.
            TypeDescriptor.AddAttributes(typeof(PostageBatchId), new TypeConverterAttribute(typeof(PostageBatchIdTypeConverter)));
            TypeDescriptor.AddAttributes(typeof(SwarmAddress), new TypeConverterAttribute(typeof(SwarmAddressTypeConverter)));
            TypeDescriptor.AddAttributes(typeof(SwarmHash), new TypeConverterAttribute(typeof(SwarmHashTypeConverter)));
            TypeDescriptor.AddAttributes(typeof(SwarmUri), new TypeConverterAttribute(typeof(SwarmUriTypeConverter)));
            TypeDescriptor.AddAttributes(typeof(TagId), new TypeConverterAttribute(typeof(TagIdTypeConverter)));

            // Configure Asp.Net Core framework services.
            services.AddControllers().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                options.JsonSerializerOptions.Converters.Add(new PostageBatchIdJsonConverter());
                options.JsonSerializerOptions.Converters.Add(new SwarmAddressJsonConverter());
                options.JsonSerializerOptions.Converters.Add(new SwarmHashJsonConverter());
                options.JsonSerializerOptions.Converters.Add(new SwarmUriJsonConverter());
                options.JsonSerializerOptions.Converters.Add(new TagIdJsonConverter());
            });
            services.AddCors();
            services.AddRazorPages();
            
            // Configure APIs.
            services.AddApiVersioning(options =>
            {
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.DefaultApiVersion = new ApiVersion(1);
                options.ReportApiVersions = true;
            });
            services.AddApiVersioning()
                .AddApiExplorer(options =>
                {
                    // add the versioned api explorer, which also adds IApiVersionDescriptionProvider service
                    // note: the specified format code will format the version as "'v'major[.minor][-status]"
                    options.GroupNameFormat = "'v'VVV";

                    // note: this option is only necessary when versioning by url segment. the SubstitutionFormat
                    // can also be used to control the format of the API version in route templates
                    options.SubstituteApiVersionInUrl = true;
                });
            
            // Configure reverse proxy.
            services.AddHttpForwarder();

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

            // Configure Swagger services.
            services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
            services.AddSwaggerGen(options =>
            {
                options.SupportNonNullableReferenceTypes();
                options.UseAllOfToExtendReferenceSchemas();
                options.UseInlineDefinitionsForEnums();

                //add a custom operation filter which sets default values
                options.OperationFilter<SwaggerDefaultValuesFilter>();
                
                //add schema filters
                options.SchemaFilter<PostageBatchIdSchemaFilter>();
                options.SchemaFilter<SwarmAddressSchemaFilter>();
                options.SchemaFilter<SwarmHashSchemaFilter>();
                options.SchemaFilter<SwarmUriSchemaFilter>();
                options.SchemaFilter<TagIdSchemaFilter>();

                //integrate xml comments
                var xmlFile = typeof(Program).GetTypeInfo().Assembly.GetName().Name + ".xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                options.IncludeXmlComments(xmlPath);
            });

            // Configure options.
            services.Configure<CashoutAllNodesChequesOptions>(config.GetSection(CashoutAllNodesChequesOptions.ConfigPosition));
            services.Configure<NodesAddressMaintainerOptions>(config.GetSection(NodesAddressMaintainerOptions.ConfigPosition));
            services.Configure<NodesChequebookMaintainerOptions>(config.GetSection(NodesChequebookMaintainerOptions.ConfigPosition));
            services.Configure<SeedDbOptions>(config.GetSection(SeedDbOptions.ConfigPosition));

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

            // Configure domain services and tools.
            services.AddDomainServices();
            services.AddTransient<IBeehiveChunkStore, BeehiveChunkStore>();
            services.AddTransient<IDbChunkStore, DbChunkStore>();
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

            // Add Hangfire.
            app.UseHangfireDashboard(
                CommonConsts.HangfireAdminPath,
                new Hangfire.DashboardOptions { Authorization = [new Configs.Hangfire.AllowAllFilter()] });

            // Add Swagger and SwaggerUI.
            var apiProvider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.DocumentTitle = "Beehive API";

                // build a swagger endpoint for each discovered API version
                foreach (var description in apiProvider.ApiVersionDescriptions.Reverse())
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
