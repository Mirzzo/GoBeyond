using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Interfaces;
using GoBeyond.Infrastructure.Services;
using GoBeyond.Infrastructure.StateMachineServices.TrainingPlans;
using GoBeyond.Infrastructure.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GoBeyond.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGoBeyondInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        services.AddDbContext<GoBeyondDbContext>(options =>
        {
            options.UseSqlServer(configuration.GetConnectionString("MainDb"));
        });

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        services.AddScoped<IDatabaseSeeder, DatabaseSeeder>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IPasswordHasherService, PasswordHasherService>();

        services.AddScoped<ITrainingPlanState, DraftTrainingPlanState>();
        services.AddScoped<ITrainingPlanState, PublishedTrainingPlanState>();
        services.AddScoped<TrainingPlanStateFactory>();

        return services;
    }
}
