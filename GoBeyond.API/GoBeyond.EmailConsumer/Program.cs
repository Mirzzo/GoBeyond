using GoBeyond.Contracts;
using GoBeyond.Contracts.Configuration;
using GoBeyond.EmailConsumer;
using GoBeyond.EmailConsumer.Options;
using GoBeyond.EmailConsumer.Services;

// Lokalno pokretanje čita root .env kao i docker-compose (već postavljene varijable imaju prednost).
var dotEnv = DotEnvFile.Load();

var builder = Host.CreateApplicationBuilder(args);

// Isti jedini izvor konfiguracije kao API: appsettings.Shared.json (+ environment varijable iz docker-compose / .env).
builder.Configuration.AddSharedConfiguration();

builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddSingleton<SentMessageIdStore>();
builder.Services.AddSingleton<InFlightSendGate>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
if (dotEnv.Path is not null)
    host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("GoBeyond.EmailConsumer")
        .LogInformation("Loaded {Loaded} setting(s) from {Path}; {Skipped} were already set in the environment.",
            dotEnv.Loaded, dotEnv.Path, dotEnv.Skipped);

await host.RunAsync();
