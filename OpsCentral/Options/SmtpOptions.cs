namespace OpsCentral.Options;

/// <summary>cloudsmtp.sgofc.com is an internal open relay: no auth, no TLS, port 25.</summary>
public class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 25;
    public string FromAddress { get; set; } = string.Empty;
}
