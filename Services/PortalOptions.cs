namespace SelfCarePortal.Services;

public class ActiveDirectoryOptions
{
    public string Domain { get; init; } = string.Empty;
    public bool AutoProvisionUsers { get; init; }
}

public class SmsGatewayOptions
{
    public string SendSmsUrl { get; init; } = string.Empty;
    public string Originator { get; init; } = string.Empty;
    public string ServiceId { get; init; } = string.Empty;
}

public class CrmGatewayOptions
{
    public string WebServiceUrl { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string AccessKey { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
}

public class BrmGatewayOptions
{
    public string AuthTokenUrl { get; init; } = string.Empty;
    public string DashboardUrl { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string AuthReferenceId { get; init; } = "123456";
    public string DashboardReferenceId { get; init; } = "1234";
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string AuthIpAddress { get; init; } = "::";
    public string DashboardIpAddress { get; init; } = string.Empty;
    public int NumberOfDisplaySi { get; init; } = 50;
    public string FromDate { get; init; } = "2020-05-01 00:00:00";
    public string ToDate { get; init; } = "2026-12-31 00:00:00";
}
