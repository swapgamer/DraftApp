using DraftDatastore.Application.Authentication;
using DraftDatastore.Infrastructure.Authentication;
using DraftDatastore.Persistence;
using DraftDatastore.Application.Players;
using DraftDatastore.Infrastructure.Players;
using DraftDatastore.Application.Admin;
using DraftDatastore.Infrastructure.Admin;
using DraftDatastore.Application.Chatbot;
using DraftDatastore.Infrastructure.Chatbot;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DraftDatastore.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DraftDatastore") ?? throw new InvalidOperationException("ConnectionStrings:DraftDatastore is required.");
        services.AddDbContext<DraftDatastoreDbContext>(options => options.UseSqlServer(connectionString, sql =>
            sql.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null)));
        services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.SectionName).Validate(x => !string.IsNullOrWhiteSpace(x.Issuer) && !string.IsNullOrWhiteSpace(x.Audience) && x.SigningKey.Length >= 32 && x.AccessTokenLifetimeMinutes is > 0 and <= 60, "Jwt configuration is invalid.").ValidateOnStart();
        services.AddScoped<IIdentityStore, EfIdentityStore>();
        services.AddScoped<IPasswordService, AspNetPasswordService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPlayerRepository, EfPlayerRepository>();
        services.AddScoped<IPlayerService, PlayerService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddOptions<OpenAiOptions>().BindConfiguration(OpenAiOptions.SectionName);
        services.AddHttpClient<IChatbotService, ChatbotService>(client =>
        {
            client.BaseAddress = new Uri("https://api.openai.com/");
            client.Timeout = TimeSpan.FromSeconds(45);
        });
        return services;
    }
}



