using GoBeyond.Contracts;
using GoBeyond.Infrastructure.BackgroundServices;
using GoBeyond.Infrastructure.Configuration;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Security;
using GoBeyond.Infrastructure.Services.Activity;
using GoBeyond.Infrastructure.Services.Admin;
using GoBeyond.Infrastructure.Services.Auth;
using GoBeyond.Infrastructure.Services.Files;
using GoBeyond.Infrastructure.Services.Mentors;
using GoBeyond.Infrastructure.Services.Messages;
using GoBeyond.Infrastructure.Services.Notifications;
using GoBeyond.Infrastructure.Services.Payments;
using GoBeyond.Infrastructure.Services.Plans;
using GoBeyond.Infrastructure.Services.Progress;
using GoBeyond.Infrastructure.Services.Recommendations;
using GoBeyond.Infrastructure.Services.ReferenceData;
using GoBeyond.Infrastructure.Services.Reports;
using GoBeyond.Infrastructure.Services.Reviews;
using GoBeyond.Infrastructure.Services.Subscriptions;
using GoBeyond.Infrastructure.Services.Users;
using GoBeyond.Infrastructure.StateMachineServices.TrainingPlans;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GoBeyond.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGoBeyondInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Konfiguracija (jedini izvor: appsettings.Shared.json + environment varijable)
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<PaymentOptions>(configuration.GetSection(PaymentOptions.SectionName));
        services.Configure<LifecycleOptions>(configuration.GetSection(LifecycleOptions.SectionName));
        services.Configure<UploadOptions>(configuration.GetSection(UploadOptions.SectionName));
        services.Configure<ActivityOptions>(configuration.GetSection(ActivityOptions.SectionName));
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));

        services.AddDbContext<GoBeyondDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("MainDb")));
        services.AddScoped<IDatabaseSeeder, DatabaseSeeder>();
        services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();

        // Sigurnost
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        // Generički BaseCRUD servisi za šifarnike
        services.AddScoped<ITrainingTypeService, TrainingTypeService>();
        services.AddScoped<IFitnessGoalService, FitnessGoalService>();
        services.AddScoped<IFitnessLevelService, FitnessLevelService>();
        services.AddScoped<IGenderService, GenderService>();

        // State Machine za trening plan
        services.AddSingleton<BaseTrainingPlanState, DraftTrainingPlanState>();
        services.AddSingleton<BaseTrainingPlanState, PublishedTrainingPlanState>();
        services.AddSingleton<BaseTrainingPlanState, ArchivedTrainingPlanState>();
        services.AddSingleton<ITrainingPlanStateFactory, TrainingPlanStateFactory>();

        // Poslovni servisi
        services.AddHttpClient<IPaymentGateway, StripePaymentGateway>();
        services.AddScoped<IFileStorageService, FileStorageService>();
        services.AddScoped<INotificationSender, NotificationSender>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IUserAccountValidator, UserAccountValidator>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<IAdminMentorService, AdminMentorService>();
        services.AddScoped<IAnnouncementService, AnnouncementService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<ISubscriptionWorkflow, SubscriptionWorkflow>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<ISubscriptionLifecycleProcessor, SubscriptionLifecycleProcessor>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<ICollaborationService, CollaborationService>();
        services.AddScoped<IMentorCertificateService, MentorCertificateService>();
        services.AddScoped<ICertificateFileService, CertificateFileService>();
        services.AddScoped<IMentorCatalogService, MentorCatalogService>();
        services.AddScoped<IRecommendationService, RecommendationService>();
        services.AddScoped<ITrainingPlanService, TrainingPlanService>();
        services.AddScoped<IProgressService, ProgressService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IMessageService, MessageService>();
        services.AddScoped<IActivityService, ActivityService>();

        // Pozadinski servisi (outbox → RabbitMQ, životni ciklus pretplata)
        services.AddHostedService<OutboxDispatcher>();
        services.AddHostedService<SubscriptionLifecycleService>();

        return services;
    }
}
