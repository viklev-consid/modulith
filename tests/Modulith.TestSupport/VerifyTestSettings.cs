namespace Modulith.TestSupport;

public static class VerifyTestSettings
{
    public static VerifySettings Create()
    {
        var settings = new VerifySettings();
        ApplyTo(settings);
        return settings;
    }

    public static void ApplyTo(VerifySettings settings)
    {
        settings.UseDirectory("Snapshots");
        settings.ScrubMember("traceId");
        settings.ScrubMember("TraceId");
        settings.ScrubMember("requestId");
        settings.ScrubMember("RequestId");
        settings.ScrubMember("timestamp");
        settings.ScrubMember("Timestamp");
        settings.ScrubMember("createdAt");
        settings.ScrubMember("CreatedAt");
        settings.ScrubMember("updatedAt");
        settings.ScrubMember("UpdatedAt");
    }
}
