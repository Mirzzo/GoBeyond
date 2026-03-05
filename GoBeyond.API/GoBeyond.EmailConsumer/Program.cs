using GoBeyond.EmailConsumer;
using GoBeyond.EmailConsumer.Consumers;
using GoBeyond.EmailConsumer.Options;
using GoBeyond.EmailConsumer.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.AddSingleton<IEmailSender, ConsoleEmailSender>();
builder.Services.AddTransient<SubscriptionActivatedConsumer>();
builder.Services.AddTransient<TrainingPlanPublishedConsumer>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
