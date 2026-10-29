using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RecordingApp.Api.Auth;
using RecordingApp.Domain;
using RecordingApp.Infrastructure;
using RecordingApp.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "JWT bearer token",
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
    });
});

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

var jwtSection = builder.Configuration.GetSection("Jwt");
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["SigningKey"]!)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
builder.Services.AddAuthorization();

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod());
});

// The API runs behind a reverse proxy (Dokploy's Traefik), so without this
// RemoteIpAddress is the proxy's address for every request and the per-IP OTP
// rate limit below became one bucket shared by all users. ForwardLimit = 1
// takes only the address the proxy itself appended, so a client can't spoof
// its own X-Forwarded-For to dodge the limit.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Partitioned per client IP so one client hammering the endpoint can't lock
    // everyone else out of requesting/verifying OTP codes. Generous enough for
    // a team of collectors signing in together behind one school/church NAT;
    // brute force is separately capped by Otp:MaxAttempts per code.
    options.AddPolicy("otp", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(1),
            PermitLimit = 30,
            QueueLimit = 0,
        }));
});

var app = builder.Build();

app.UseForwardedHeaders();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RecordingAppDbContext>();
    db.Database.Migrate();

    // There's no password login and invites can only be sent by an existing
    // Admin, so the very first Admin has to be created some other way. If none
    // exists yet, seed one from config (BOOTSTRAP_ADMIN_EMAIL / _PHONENUMBER) so
    // they can sign in with the normal OTP flow and invite everyone else.
    if (!await db.Users.AnyAsync(u => u.Roles.Any(r => r.Role == UserRole.Admin)))
    {
        var bootstrapEmail = builder.Configuration["Bootstrap:AdminEmail"];
        var bootstrapPhone = builder.Configuration["Bootstrap:AdminPhoneNumber"];
        if (!string.IsNullOrWhiteSpace(bootstrapEmail) || !string.IsNullOrWhiteSpace(bootstrapPhone))
        {
            var admin = new User
            {
                Email = Identifiers.NormalizeEmail(bootstrapEmail),
                PhoneNumber = Identifiers.NormalizePhone(bootstrapPhone),
                Status = UserStatus.Active,
                ActivatedAt = DateTimeOffset.UtcNow,
            };
            admin.Roles.Add(new UserRoleAssignment { UserId = admin.Id, Role = UserRole.Admin });
            db.Users.Add(admin);
            await db.SaveChangesAsync();
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
