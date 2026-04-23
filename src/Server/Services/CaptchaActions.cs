namespace Server.Services;

/// <summary>reCAPTCHA Enterprise v3 action names; must match DefaultAction / ExecuteV3 on the client.</summary>
public static class CaptchaActions
{
    public const string BoardPost = "board_post";
    public const string BoardComment = "board_comment";
    public const string PackageReview = "package_review";
}
