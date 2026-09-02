using AzmCrm.Application.Localization;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Features.Identity;
using AzmCrm.Infrastructure.Communications;
using AzmCrm.Infrastructure.Data;
using AzmCrm.Infrastructure.Identity;
using AzmCrm.Infrastructure.Localization;
using AzmCrm.Infrastructure.Sla;
using AzmCrm.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace AzmCrm.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<ApplicationDbContext>());

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));

        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;

            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;

            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()!;

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtSettings.Secret)),
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudiences = new[]
                {
                    "AzmCrm"
                },
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            // SignalR's browser client cannot attach an Authorization header to the WebSocket
            // handshake, so it passes the JWT as an "access_token" query-string parameter
            // instead — this is ASP.NET Core's documented pattern for authenticating a hub
            // connection. Restricted to the /hubs path prefix so it never weakens how a normal
            // REST Authorization header is validated for every other endpoint.
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;

                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                        context.Token = accessToken;

                    return Task.CompletedTask;
                }
            };
        });

        services.AddAuthorization();

        services.AddMemoryCache();
        services.AddScoped<ILocalizationService, LocalizationService>();

        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();
        services.AddScoped<IIdentityQueryService, IdentityQueryService>();

        services.Configure<FileStorageSettings>(configuration.GetSection(FileStorageSettings.SectionName));
        services.AddScoped<IFileStorageService, LocalFileStorageService>();

        services.Configure<SmtpSettings>(configuration.GetSection(SmtpSettings.SectionName));
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<IChannelMessageSender, EmailChannelMessageSender>();

        services.Configure<WhatsAppSettings>(configuration.GetSection(WhatsAppSettings.SectionName));
        services.AddHttpClient<WhatsAppCloudApiProvider>();
        services.AddScoped<IWhatsAppProvider>(provider => provider.GetRequiredService<WhatsAppCloudApiProvider>());
        services.AddScoped<IChannelMessageSender, WhatsAppChannelMessageSender>();

        services.Configure<SmsSettings>(configuration.GetSection(SmsSettings.SectionName));
        services.AddHttpClient<SmsGatewayProvider>();
        services.AddScoped<ISmsProvider>(provider => provider.GetRequiredService<SmsGatewayProvider>());
        services.AddScoped<IChannelMessageSender, SmsChannelMessageSender>();

        services.AddHttpContextAccessor();

        services.Configure<SlaMonitoringSettings>(configuration.GetSection(SlaMonitoringSettings.SectionName));
        services.AddHostedService<SlaMonitoringBackgroundService>();

        return services;
    }
}
