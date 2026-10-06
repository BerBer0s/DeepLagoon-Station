namespace Content.Client._DeepLagoon.WebUI;

/// <summary>Explicitly configured loopback origins only. Never enables remote wiki access.</summary>
public static class TguiDevelopmentPolicy
{
    public static string? GetOrigin(string configured)
    {
        if (!WikiNavigationPolicy.TryUri(configured, out var uri) || uri.Scheme != "http" ||
            uri.Host is not ("127.0.0.1" or "localhost") || uri.Port is < 1024 or > 65535 ||
            uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.AbsolutePath != "/")
            return null;
        return "http://" + uri.Host + ":" + uri.Port;
    }

    public static bool IsResource(Uri uri, string? origin) => origin != null &&
        uri.UserInfo.Length == 0 && (uri.Scheme == "http" || uri.Scheme == "ws") &&
        origin == "http://" + uri.Host + ":" + uri.Port;

    public static bool IsDocument(Uri uri, string? origin) => uri.Scheme == "http" &&
        IsResource(uri, origin) && uri.AbsolutePath is "/interface.html" or "/chat.html";
}
