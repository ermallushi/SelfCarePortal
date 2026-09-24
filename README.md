# SelfCarePortal

ASP.NET Core MVC prototype for the ONE Business Portal.

## Run locally

```bash
dotnet restore
dotnet run
```

## Configure integrations

Set the integration values through configuration providers such as environment variables or user secrets before exercising the OTP and CRM/BRM flows.

Required sections:
- `ActiveDirectory:Domain`
- `ActiveDirectory:AutoProvisionUsers`
- `SmsGateway:SendSmsUrl`
- `SmsGateway:Originator`
- `SmsGateway:ServiceId`
- `CrmGateway:WebServiceUrl`
- `CrmGateway:Username`
- `CrmGateway:AccessKey`
- `CrmGateway:Source`
- `BrmGateway:AuthTokenUrl`
- `BrmGateway:DashboardUrl`
- `BrmGateway:Source`
- `BrmGateway:ReferenceId`
- `BrmGateway:Username`
- `BrmGateway:Password`
- `BrmGateway:IpAddress`
- `BrmGateway:NumberOfDisplaySi`
- `BrmGateway:FromDate`
- `BrmGateway:ToDate`

After configuration, open the local URL shown by ASP.NET Core, request an OTP for a customer mobile number, verify the code, and the portal will load the CRM profile plus the BRM/CRM-derived line inventory.
