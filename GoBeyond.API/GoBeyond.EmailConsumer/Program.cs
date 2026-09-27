using GoBeyond.Contracts;
using GoBeyond.Contracts.Configuration;
using GoBeyond.EmailConsumer;
using GoBeyond.EmailConsumer.Options;
using GoBeyond.EmailConsumer.Services;

var builder = Host.CreateApplicationBuilder(args);

// Isti jedini izvor konfiguracije kao API: appsettings.Shared.json (+ environment varijable iz docker-compose).
builder.Configuration.AddSharedConfiguration();

builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddHostedService<Worker>();

await builder.Build().RunAsync();
