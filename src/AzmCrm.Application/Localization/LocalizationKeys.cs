namespace AzmCrm.Application.Localization;

public static class LocalizationKeys
{
    public static class Validation
    {
        public const string Required = "Validation.Required";
        public const string EmailInvalid = "Validation.EmailInvalid";
        public const string MinLength = "Validation.MinLength";
        public const string MaxLength = "Validation.MaxLength";
        public const string PasswordTooWeak = "Validation.PasswordTooWeak";
        public const string PasswordsDoNotMatch = "Validation.PasswordsDoNotMatch";
        public const string InvalidPhoneNumber = "Validation.InvalidPhoneNumber";
        public const string MustBeGreaterThan = "Validation.MustBeGreaterThan";
        public const string MustBeLessThan = "Validation.MustBeLessThan";
        public const string UsernamePattern = "Validation.UsernamePattern";
        public const string IdMismatch = "Validation.IdMismatch";
        public const string InvalidValue = "Validation.InvalidValue";
        public const string FileTooLarge = "Validation.FileTooLarge";
    }

    public static class Identity
    {
        public const string UsernameTaken = "Identity.UsernameTaken";
        public const string EmailAlreadyRegistered = "Identity.EmailAlreadyRegistered";
        public const string MobileNumberAlreadyRegistered = "Identity.MobileNumberAlreadyRegistered";
        public const string InvalidCredentials = "Identity.InvalidCredentials";
        public const string AccountInactive = "Identity.AccountInactive";
        public const string InvalidRefreshToken = "Identity.InvalidRefreshToken";
        public const string RefreshTokenNotActive = "Identity.RefreshTokenNotActive";
        public const string TokenRevoked = "Identity.TokenRevoked";
        public const string UserNotFound = "Identity.UserNotFound";
        public const string UserNotAuthenticated = "Identity.UserNotAuthenticated";
    }

    public static class Common
    {
        public const string OperationSuccessful = "Common.OperationSuccessful";
        public const string OperationFailed = "Common.OperationFailed";
        public const string UnexpectedError = "Common.UnexpectedError";
        public const string NotFound = "Common.NotFound";
        public const string Unauthorized = "Common.Unauthorized";
        public const string Forbidden = "Common.Forbidden";
        public const string ValidationFailed = "Common.ValidationFailed";
    }
}
