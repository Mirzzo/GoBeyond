using GoBeyond.EmailConsumer;
using GoBeyond.Contracts;
using GoBeyond.Contracts.Configuration;
using GoBeyond.EmailConsumer.Options;
using GoBeyond.EmailConsumer.Services;

EnvironmentFile.Load();
var builder = Host.CreateApplicationBuilder(args);
builder.Configuration
    .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.Shared.json"), optional: false)
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables().AddCommandLine(args);

builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
