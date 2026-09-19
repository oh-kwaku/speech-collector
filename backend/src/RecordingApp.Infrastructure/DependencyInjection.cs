using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RecordingApp.Infrastructure.Export;
using RecordingApp.Infrastructure.Notifications;
using RecordingApp.Infrastructure.Security;
using RecordingApp.Infrastructure.Storage;

namespace RecordingApp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<RecordingAppDbContext>(opt =>
            opt.UseNpgsql(config.GetConnectionString("Postgres")));

        services.Configure<JwtOptions>(config.GetSection("Jwt"));
        services.Configure<S3Options>(config.GetSection("S3"));
        services.Configure<SmtpOptions>(config.GetSection("Smtp"));
        services.Configure<OtpOptions>(config.GetSection("Otp"));

        services.AddSingleton<IAmazonS3>(sp =>
        {
            var s3 = sp.GetRequiredService<IOptions<S3Options>>().Value;

            var clientConfig = new AmazonS3Config { ForcePathStyle = s3.ForcePathStyle };
            if (!string.IsNullOrWhiteSpace(s3.ServiceUrl))
                clientConfig.ServiceURL = s3.ServiceUrl;
            else if (!string.IsNullOrWhiteSpace(s3.Region))
                clientConfig.RegionEndpoint = RegionEndpoint.GetBySystemName(s3.Region);

            return !string.IsNullOrWhiteSpace(s3.AccessKey) && !string.IsNullOrWhiteSpace(s3.SecretKey)
                ? new AmazonS3Client(new BasicAWSCredentials(s3.AccessKey, s3.SecretKey), clientConfig)
                : new AmazonS3Client(clientConfig);
        });

        services.AddScoped<IStorageService, S3StorageService>();
        services.AddScoped<IPhotoSyncService, PhotoSyncService>();
        services.AddScoped<IExportService, ExportService>();
        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IEmailSender, GmailSmtpEmailSender>();
        services.AddScoped<ISmsSender, ConsoleSmsSender>();

        return services;
    }
}
