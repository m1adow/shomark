namespace ShoMark.Application.Common;

public static partial class Constants
{
    public static partial class Errors
    {
        public static class Codes
        {
            public const string NotFound = "NOT_FOUND";
            public const string Validation = "VALIDATION_ERROR";
            public const string Duplicate = "DUPLICATE";
            public const string StorageError = "STORAGE_ERROR";

            // OAuth / Token
            public const string InvalidState = "INVALID_STATE";
            public const string OAuthExchangeFailed = "OAUTH_EXCHANGE_FAILED";
            public const string NoAccessToken = "NO_ACCESS_TOKEN";
            public const string NoRefreshToken = "NO_REFRESH_TOKEN";
            public const string TokenExpired = "TOKEN_EXPIRED";
            public const string RefreshFailed = "REFRESH_FAILED";

            // Publishing
            public const string AlreadyPublished = "ALREADY_PUBLISHED";
            public const string PlatformNotFound = "PLATFORM_NOT_FOUND";
            public const string UnsupportedPlatform = "UNSUPPORTED_PLATFORM";
            public const string PublishFailed = "PUBLISH_FAILED";
        }
    }
}
