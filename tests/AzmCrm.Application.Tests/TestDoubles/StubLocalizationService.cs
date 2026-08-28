using AzmCrm.Application.Localization;

namespace AzmCrm.Application.Tests.TestDoubles;

/// <summary>
/// Hand-written <see cref="ILocalizationService"/> stub for validator tests. Returns the raw
/// key (formatted with args, if any) instead of a localized string, so tests can assert on the
/// key rather than on translated text.
/// </summary>
public sealed class StubLocalizationService : ILocalizationService
{
    public string this[string key] => key;
    public string this[string key, params object[] args] => string.Format(key, args);
    public string GetString(string key) => key;
    public string GetString(string key, params object[] args) => string.Format(key, args);
    public string GetCurrentLanguage() => "en";
    public void SetLanguage(string culture) { }
}
