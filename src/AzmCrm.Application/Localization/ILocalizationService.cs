namespace AzmCrm.Application.Localization;

public interface ILocalizationService
{
    string this[string key] { get; }
    string this[string key, params object[] args] { get; }
    string GetString(string key);
    string GetString(string key, params object[] args);
    string GetCurrentLanguage();
    void SetLanguage(string culture);
}
