namespace Server.Features.Auth;

public sealed record RequestPasswordResetRequest(string Email);

public sealed record ResetPasswordRequest(string UserId, string Token, string NewPassword);
