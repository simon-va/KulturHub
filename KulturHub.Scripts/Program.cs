using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KulturHub.Scripts.SetAuthToken;

public static class Program
{
    private const string DefaultBaseUrl = "http://localhost:5159";
    private const string DefaultEmail = "max@example.com";
    private const string DefaultPassword = "Sicher123!";

    public static async Task<int> Main(string[] args)
    {
        var baseUrl = GetArg(args, "--baseUrl") ?? DefaultBaseUrl;
        var email = GetArg(args, "--email") ?? DefaultEmail;
        var password = GetArg(args, "--password") ?? DefaultPassword;
        var environmentName = GetArg(args, "--env") ?? "dev";

        var envFileArg = GetArg(args, "--envFile");
        var resolvedEnvPath = ResolveEnvFilePath(envFileArg);
        if (!File.Exists(resolvedEnvPath))
        {
            Console.Error.WriteLine($"env file not found: {resolvedEnvPath}");
            return 1;
        }

        using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };

        var signInUrl = new Uri(new Uri(baseUrl), "/auth/signin");
        Console.WriteLine($"POST {signInUrl} as {email} ...");

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync(signInUrl, new { email, password });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"sign-in request failed: {ex.Message}");
            return 2;
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            Console.Error.WriteLine(
                $"sign-in failed: {(int)response.StatusCode} {response.ReasonPhrase}{Environment.NewLine}{body}");
            return 3;
        }

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        if (!payload.TryGetProperty("accessToken", out var accessTokenElement)
            || accessTokenElement.ValueKind != JsonValueKind.String)
        {
            Console.Error.WriteLine("sign-in response did not contain 'accessToken' as string.");
            return 4;
        }

        var accessToken = accessTokenElement.GetString()!;
        UpdateEnvFile(resolvedEnvPath, environmentName, "token", accessToken);

        Console.WriteLine($"token written to {resolvedEnvPath} (env: {environmentName}).");
        return 0;
    }

    private static string ResolveEnvFilePath(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
            return Path.GetFullPath(explicitPath);

        var scriptDir = AppContext.BaseDirectory;
        var probe = scriptDir;
        while (probe is not null)
        {
            var candidate = Path.Combine(probe, "KulturHub.Api", "http", "http-client.env.json");
            if (File.Exists(candidate))
                return candidate;

            probe = Path.GetDirectoryName(probe);
        }

        return Path.GetFullPath(Path.Combine(scriptDir, "..", "..", "..", "..", "KulturHub.Api", "http", "http-client.env.json"));
    }

    private static string? GetArg(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return null;
    }

    private static void UpdateEnvFile(string path, string environmentName, string key, string value)
    {
        var rawJson = File.ReadAllText(path);
        var nodeOptions = new JsonNodeOptions { PropertyNameCaseInsensitive = false };
        var docOptions = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        using var document = JsonDocument.Parse(rawJson, docOptions);
        var root = JsonNode.Parse(document.RootElement.GetRawText(), nodeOptions)!;

        if (root[environmentName] is not JsonObject envSection)
        {
            Console.Error.WriteLine($"environment '{environmentName}' not present in {path}.");
            Environment.Exit(5);
            return;
        }

        envSection[key] = value;

        var serializerOptions = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(path, root.ToJsonString(serializerOptions) + Environment.NewLine);
    }
}