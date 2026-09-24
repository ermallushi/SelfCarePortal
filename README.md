# SelfCarePortal

ASP.NET Core MVC prototype for the ONE Business Portal.

## Run locally

```bash
dotnet restore
dotnet run
```

## Configure integrations

The repository now commits the requested non-secret endpoint defaults for the OTP, CRM and BRM integrations in `appsettings.json`.

You still need to provide the secret values through configuration providers such as environment variables or user secrets before exercising the full flow:
- `SmsGateway:ServiceId`
- `CrmGateway:AccessKey`
- `BrmGateway:Password`

Other committed sections include:
- `ActiveDirectory:Domain`
- `ActiveDirectory:AutoProvisionUsers`
- `SmsGateway:SendSmsUrl`
- `SmsGateway:Originator`
- `CrmGateway:WebServiceUrl`
- `CrmGateway:Username`
- `CrmGateway:Source`
- `BrmGateway:AuthTokenUrl`
- `BrmGateway:DashboardUrl`
- `BrmGateway:Source`
- `BrmGateway:AuthReferenceId`
- `BrmGateway:DashboardReferenceId`
- `BrmGateway:Username`
- `BrmGateway:AuthIpAddress`
- `BrmGateway:DashboardIpAddress`
- `BrmGateway:NumberOfDisplaySi`
- `BrmGateway:FromDate`
- `BrmGateway:ToDate`

After configuration, open the local URL shown by ASP.NET Core, request an OTP for a customer mobile number, verify the code, and the portal will load the CRM profile plus the BRM/CRM-derived line inventory.
