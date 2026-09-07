using System.Globalization;
using DraftDatastore.Domain.Entities;
using System.Text;
using System.Threading.RateLimiting;
using DraftDatastore.Application.Authentication;
using DraftDatastore.Infrastructure;
using DraftDatastore.Infrastructure.Authentication;
using DraftDatastore.API.Filters;
using DraftDatastore.API.Middleware;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;
using DraftDatastore.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration.ReadFrom.Configuration(context.Configuration).ReadFrom.Services(services).Enrich.FromLogContext().WriteTo.Console(formatProvider: CultureInfo.InvariantCulture));

builder.Services.AddScoped<RequestValidationFilter>();
builder.Services.AddTransient<ExceptionHandlingMiddleware>();
builder.Services.AddControllers(options => options.Filters.AddService<RequestValidationFilter>());
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => { options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Name="Authorization", Type=SecuritySchemeType.Http, Scheme="bearer", BearerFormat="JWT", In=ParameterLocation.Header, Description="Enter a valid JWT bearer token." }); options.AddSecurityRequirement(new OpenApiSecurityRequirement { { new OpenApiSecurityScheme { Reference=new OpenApiReference { Type=ReferenceType.SecurityScheme, Id="Bearer" } }, Array.Empty<string>() } }); });
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
builder.Services.AddInfrastructure(builder.Configuration);

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? throw new InvalidOperationException("Jwt configuration is required.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwt.Issuer,
        ValidateAudience = true,
        ValidAudience = jwt.Audience,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
        ClockSkew = TimeSpan.FromMinutes(1)
    };
});
builder.Services.AddAuthorization(options => options.AddPolicy(SystemRoles.Admin, policy => policy.RequireRole(SystemRoles.Admin)));
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? []).AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddRateLimiter(options => { options.RejectionStatusCode = StatusCodes.Status429TooManyRequests; options.AddFixedWindowLimiter("auth", limiter => { limiter.PermitLimit = 10; limiter.Window = TimeSpan.FromMinutes(1); limiter.QueueLimit = 0; }); options.AddFixedWindowLimiter("chat", limiter => { limiter.PermitLimit = 30; limiter.Window = TimeSpan.FromMinutes(1); limiter.QueueLimit = 0; }); });
builder.Services.AddHealthChecks();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DraftDatastoreDbContext>();
    var emails = builder.Configuration.GetSection("Administration:SystemAdminEmails").Get<string[]>() ?? [];
    var permanentAdmins = await db.Users.Include(x => x.UserRoles).Where(x => emails.Contains(x.NormalizedEmail)).ToListAsync();
    foreach (var user in permanentAdmins)
    {
        user.IsSystemAdmin = true;
        user.AdminExpiresAtUtc = null;
        if (!user.UserRoles.Any(x => x.RoleId == SystemRoleIds.Admin))
        {
            user.UserRoles.Clear();
            user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = SystemRoleIds.Admin });
        }
    }
    if (permanentAdmins.Count > 0) await db.SaveChangesAsync();
}
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSerilogRequestLogging();
app.UseStaticFiles();
app.UseMiddleware<AuditLoggingMiddleware>();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapControllers();
app.Run();







