﻿using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Azure;
using Azure.Storage.Queues;
using Azure.Storage.Blobs;
using Azure.Core.Extensions;
using System;
using System.Text.Json.Serialization;

namespace BeatLeader_Server {
    public class AzureStorageConfig {
        public string AccountName { get; set; }
        public string ReplaysContainerName { get; set; }
        public string AssetsContainerName { get; set; }
    }

    public class Startup {
        static string MyAllowSpecificOrigins = "_myAllowSpecificOrigins";
        public Startup (IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices (IServiceCollection services)
        {
            services.AddAuthentication (options => {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })

            .AddCookie (options => {
                options.Events.OnRedirectToAccessDenied =
                options.Events.OnRedirectToLogin = c => {
                    c.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.FromResult<object> (null);
                };
                options.Cookie.SameSite = SameSiteMode.None;
                options.Cookie.Domain = ".beatleader.xyz";
                options.Cookie.HttpOnly = false;
            })
            //.AddSteamTicket(options =>
            //{
            //    options.Key = "B0A7AF33E804D0ABBDE43BA9DD5DAB48";
            //    options.ApplicationID = "620980";
            //})
            .AddOculus(options =>
            {
                options.Key = "B0A7AF33E804D0ABBDE43BA9DD5DAB48";
                options.ApplicationID = "620980";
            })
            .AddSteam (options => {
                options.ApplicationKey = "B0A7AF33E804D0ABBDE43BA9DD5DAB48";
                options.Events.OnAuthenticated = ctx => {
                    /* ... */
                    return Task.CompletedTask;
                };
            });


            var connection = "Data Source = tcp:localhost,1433; Initial Catalog = BeatLeader_DEBUG; User Id = sa; Password = SuperStrong!";
            services.AddDbContext<AppContext> (options => options.UseSqlServer (connection));

            services.Configure<AzureStorageConfig> (Configuration.GetSection ("AzureStorageConfig"));

            services.AddMvc ().AddControllersAsServices ().AddJsonOptions (options => {
                options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            });
            services.AddAzureClients (builder => {
                builder.AddBlobServiceClient (Configuration ["CDN:blob"], preferMsi: true);
                builder.AddQueueServiceClient (Configuration ["CDN:queue"], preferMsi: true);
            });
            services.AddCors (options => {
                options.AddPolicy (name: MyAllowSpecificOrigins,
                    builder => {
                        builder.WithOrigins("http://localhost:8888",
                                            "https://www.beatleader.xyz",
                                            "https://agitated-ptolemy-7d772c.netlify.app");
                        builder.AllowCredentials();
                    });
            });
        }

        public void Configure (IApplicationBuilder app)
        {
            app.UseStaticFiles ();

            app.UseRouting ();

            app.UseAuthentication ();
            app.UseAuthorization ();

            app.UseCors (MyAllowSpecificOrigins);

            app.UseEndpoints (endpoints => {
                endpoints.MapDefaultControllerRoute ();
            });
        }
    }
    internal static class StartupExtensions {
        public static IAzureClientBuilder<BlobServiceClient, BlobClientOptions> AddBlobServiceClient (this AzureClientFactoryBuilder builder, string serviceUriOrConnectionString, bool preferMsi)
        {
            if (preferMsi && Uri.TryCreate (serviceUriOrConnectionString, UriKind.Absolute, out Uri serviceUri)) {
                return builder.AddBlobServiceClient (serviceUri);
            } else {
                return builder.AddBlobServiceClient (serviceUriOrConnectionString);
            }
        }
        public static IAzureClientBuilder<QueueServiceClient, QueueClientOptions> AddQueueServiceClient (this AzureClientFactoryBuilder builder, string serviceUriOrConnectionString, bool preferMsi)
        {
            if (preferMsi && Uri.TryCreate (serviceUriOrConnectionString, UriKind.Absolute, out Uri serviceUri)) {
                return builder.AddQueueServiceClient (serviceUri);
            } else {
                return builder.AddQueueServiceClient (serviceUriOrConnectionString);
            }
        }
    }
}
