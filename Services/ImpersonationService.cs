namespace MyHotel.Web.Services;

public class ImpersonationService
{
    private readonly IHttpContextAccessor _http;

    public ImpersonationService(IHttpContextAccessor http)
    {
        _http = http;
    }

    public bool IsImpersonating => _http.HttpContext?.Session.GetString("ImpersonatingUserId") != null;

    public string? ImpersonatedUserId => _http.HttpContext?.Session.GetString("ImpersonatingUserId");

    public string? OriginalUserId => _http.HttpContext?.Session.GetString("OriginalUserId");

    public void StartImpersonation(string originalUserId, string targetUserId)
    {
        var session = _http.HttpContext?.Session;
        if (session == null) return;
        session.SetString("OriginalUserId", originalUserId);
        session.SetString("ImpersonatingUserId", targetUserId);
    }

    public void StopImpersonation()
    {
        var session = _http.HttpContext?.Session;
        if (session == null) return;
        session.Remove("OriginalUserId");
        session.Remove("ImpersonatingUserId");
    }

    public string GetEffectiveUserId(string currentUserId)
    {
        return ImpersonatedUserId ?? currentUserId;
    }
}
