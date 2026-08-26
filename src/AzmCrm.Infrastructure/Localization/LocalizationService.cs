using AzmCrm.Application.Localization;
using Microsoft.Extensions.Caching.Memory;
using System.Globalization;
using System.Text.Json;

namespace AzmCrm.Infrastructure.Localization;

internal sealed class LocalizationService : ILocalizationService
{
    private readonly IMemoryCache _cache;
    private readonly string _resourcePath;
    private string _currentLanguage = "en";

    public LocalizationService(IMemoryCache cache)
    {
        _cache = cache;
        _resourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Localization", "Resources");
    }

    public string this[string key] => GetString(key);

    public string this[string key, params object[] args] => GetString(key, args);

    public string GetString(string key)
    {
        var resources = GetResources(_currentLanguage);
        return GetValueFromNestedKey(resources, key) ?? key;
    }

    public string GetString(string key, params object[] args)
    {
        var format = GetString(key);

        try
        {
            return string.Format(format, args);
        }
        catch
        {
            return format;
        }
    }

    public string GetCurrentLanguage() => _currentLanguage;

    public void SetLanguage(string culture)
    {
        _currentLanguage = culture?.ToLower() switch
        {
            "ar" or "ar-sa" or "ar-eg" => "ar",
            "en-us" or "en" => "en",
            _ => "en"
        };

        CultureInfo.CurrentCulture = new CultureInfo(_currentLanguage);
        CultureInfo.CurrentUICulture = new CultureInfo(_currentLanguage);
    }

    private Dictionary<string, JsonElement> GetResources(string language)
    {
        var cacheKey = $"localization_{language}";

        if (_cache.TryGetValue(cacheKey, out Dictionary<string, JsonElement>? cached))
        {
            return cached!;
        }

        var filePath = Path.Combine(_resourcePath, $"Messages.{language}.json");

        if (!File.Exists(filePath))
        {
            filePath = Path.Combine(_resourcePath, "Messages.en.json");
        }

        var json = File.ReadAllText(filePath);
        var resources = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)
                        ?? [];

        _cache.Set(cacheKey, resources, TimeSpan.FromHours(24));

        return resources;
    }

    private static string? GetValueFromNestedKey(Dictionary<string, JsonElement> dict, string key)
    {
        var parts = key.Split('.');
        JsonElement current = default;
        var found = false;

        foreach (var part in parts)
        {
            if (!found)
            {
                if (dict.TryGetValue(part, out current))
                {
                    found = true;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(part, out var property))
                {
                    current = property;
                }
                else
                {
                    return null;
                }
            }
        }

        return found ? current.ToString() : null;
    }
}
