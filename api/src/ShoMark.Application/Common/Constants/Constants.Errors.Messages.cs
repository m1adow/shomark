namespace ShoMark.Application.Common;

public static partial class Constants
{
    public static partial class Errors
    {
        public static class Messages
        {
            // Video
            public const string VideoNotFound = "Video not found";
            public const string DuplicateVideo = "A video with this MinIO key already exists";
            public const string NoFileProvided = "No file provided";
            public const string InvalidFileType = "Only MP4 and MOV files are allowed";

            // Fragment
            public const string FragmentNotFound = "Fragment not found";
            public const string NoThumbnailAvailable = "No thumbnail available";
            public const string ThumbnailUrlGenerationFailed = "Failed to generate thumbnail URL";
            public const string StartTimeMustBeLessThanEndTime = "StartTime must be less than EndTime";

            // Post
            public const string PostNotFound = "Post not found";
            public const string PostAlreadyPublished = "Post already published";

            // Platform / OAuth
            public const string PlatformNotFound = "Platform not found";
            public const string PlatformNotConnected = "Platform not connected";
            public const string NoAccessToken = "No access token available";
            public const string NoRefreshToken = "No refresh token available";
            public const string TokenExpiredNoRefresh = "Token expired and no refresh token available";
            public const string InvalidOAuthState = "Invalid OAuth state parameter";

            // Campaign
            public const string CampaignNotFound = "Campaign not found";
            public const string DuplicateCampaign = "A campaign with this name already exists";

            // Notification / Analytics
            public const string NotificationNotFound = "Notification not found";
            public const string AnalyticsNotFound = "Analytics not found";

            // Publishing
            public const string UnknownPublishingError = "Unknown publishing error";
            public const string PublishingFailed = "Publishing failed";
        }
    }
}
