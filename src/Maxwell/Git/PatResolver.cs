using Microsoft.Extensions.Configuration;

public static class PatResolver
{
    public static string? Resolve(IConfiguration? config = null)
    {
        // 1. User Secrets / appsettings (solo en desarrollo)
        string? fromConfig = config?["GitHub:Pat"];
        if (!string.IsNullOrWhiteSpace(fromConfig))
            return fromConfig;

        // 2. Variable de entorno (desarrollo y producción)
        string? fromEnv = Environment.GetEnvironmentVariable("MAXWELL_GITHUB_PAT");
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv;

        // 3. No encontrado
        return null;
    }
}