namespace GoBeyond.Contracts.Configuration;

public static class EnvironmentFile
{
    public static void Load()
    {
        // Find the project root from either dotnet run or a published binary.
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                var path = Path.Combine(directory.FullName, ".env");
                if (File.Exists(path)) { LoadFile(path); break; }
                if (File.Exists(Path.Combine(directory.FullName, "docker-compose.yml"))) break;
                directory = directory.Parent;
            }
        }
        var aliases = new Dictionary<string, string>
        {
            ["JWT_SECRET_KEY"] = "Jwt__SecretKey",
            ["RABBITMQ_HOST"] = "RabbitMq__Host", ["RABBITMQ_PORT"] = "RabbitMq__Port",
            ["RABBITMQ_USERNAME"] = "RabbitMq__Username", ["RABBITMQ_PASSWORD"] = "RabbitMq__Password",
            ["RABBITMQ_QUEUE"] = "RabbitMq__Queue",
            ["SMTP_HOST"] = "Smtp__Host", ["SMTP_PORT"] = "Smtp__Port",
            ["SMTP_USERNAME"] = "Smtp__Username", ["SMTP_PASSWORD"] = "Smtp__Password",
            ["STRIPE_SECRET_KEY"] = "Payments__SecretKey", ["STRIPE_PUBLISHABLE_KEY"] = "Payments__PublishableKey",
            ["STRIPE_WEBHOOK_SECRET"] = "Payments__WebhookSecret"
        };
        foreach (var (source, target) in aliases)
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(target))
                && Environment.GetEnvironmentVariable(source) is { Length: > 0 } value)
                Environment.SetEnvironmentVariable(target, value);
    }

    private static void LoadFile(string path)
    {
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var separator = line.IndexOf('=');
            if (separator <= 0) continue;
            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
                value = value[1..^1];
            if (Environment.GetEnvironmentVariable(key) is null)
                Environment.SetEnvironmentVariable(key, value);
        }
    }
}
