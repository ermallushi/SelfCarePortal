using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using SelfCarePortal.Models;

namespace SelfCarePortal.Services;

public class PortalDataService : IPortalDataService
{
    private const int MaxOtpAttempts = 5;
    private const string PortalHttpClientName = "PortalClient";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PortalDataService> _logger;
    private readonly ActiveDirectoryOptions _activeDirectoryOptions;
    private readonly SmsGatewayOptions _smsGatewayOptions;
    private readonly CrmGatewayOptions _crmGatewayOptions;
    private readonly BrmGatewayOptions _brmGatewayOptions;
    private readonly object _stateLock = new();
    private readonly ConcurrentDictionary<string, PendingOtpState> _pendingOtps = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, FeatureState> _lineFeatures = new(StringComparer.Ordinal);
    private readonly List<AddOnOption> _addOns =
    [
        new() { Code = "ROAM-7", Name = "Roaming Saver", Description = "7-day regional roaming bundle", Price = "ALL 1,500" },
        new() { Code = "DATA-20", Name = "Extra Data 20GB", Description = "One-off data boost for the current billing cycle", Price = "ALL 2,200" },
        new() { Code = "INT-200", Name = "International 200", Description = "200 international minutes for priority destinations", Price = "ALL 1,900" }
    ];
    private readonly List<TariffPackage> _tariffs =
    [
        new() { Name = "Postpaid", Description = "Corporate postpaid service instances loaded from CRM account relationships.", BillingCycle = "Account-defined", Eligibility = "Returned from CRM / BRM service inventory" },
        new() { Name = "Voice & Data", Description = "Voice and data-capable lines loaded from the linked service instance records.", BillingCycle = "Account-defined", Eligibility = "Service instances with mobile numbers" }
    ];
    private readonly List<InvoiceSummary> _invoices =
    [
        new()
        {
            InvoiceId = "INV-2026-08-001",
            BillingPeriod = "August 2026",
            TotalAmount = 10850m,
            Status = "Paid",
            DueDate = new DateOnly(2026, 9, 10),
            StatementLines = new[]
            {
                "Core mobile bundle charges: ALL 8,600",
                "One-off data boost charges: ALL 1,200",
                "VAT: ALL 1,050"
            }
        },
        new()
        {
            InvoiceId = "INV-2026-09-001",
            BillingPeriod = "September 2026",
            TotalAmount = 12140m,
            Status = "Due",
            DueDate = new DateOnly(2026, 10, 10),
            StatementLines = new[]
            {
                "Corporate voice & data bundles: ALL 9,300",
                "Roaming add-ons: ALL 1,800",
                "VAT: ALL 1,040"
            }
        }
    ];
    private readonly List<SupportTicket> _tickets =
    [
        new() { TicketId = "TCK-4401", Subject = "Roaming activation for executive line", Priority = "High", Status = "Open", OpenedOn = new DateOnly(2026, 9, 20) },
        new() { TicketId = "TCK-4388", Subject = "Invoice clarification for IoT estate", Priority = "Medium", Status = "In Progress", OpenedOn = new DateOnly(2026, 9, 18) },
        new() { TicketId = "TCK-4310", Subject = "New branch SIM delivery", Priority = "Low", Status = "Resolved", OpenedOn = new DateOnly(2026, 9, 10) }
    ];
    private readonly SupportContact _supportContact = new()
    {
        Name = "Erisa Kola",
        Phone = "+355 69 555 0123",
        Email = "erisa.kola@one.al",
        WhatsAppLink = "https://wa.me/355695550123"
    };
    private readonly List<OfferRecommendation> _offers =
    [
        new() { Title = "Roaming Saver", Summary = "Recommended for teams with frequent travel activity across the loaded mobile estate.", Segment = "Frequent travel", Trigger = "Roaming feature usage on authenticated customer lines" },
        new() { Title = "Data Boost", Summary = "Increase data pools for business lines approaching their normal usage window.", Segment = "Branch operations", Trigger = "Postpaid mobile line portfolio present in CRM" },
        new() { Title = "Upgrade Bundle", Summary = "Align the corporate package catalog with the latest account-managed service instances.", Segment = "Enterprise", Trigger = "Account lines loaded from BRM / CRM inventory" }
    ];
    private readonly ArchitectureSummary _architecture = new()
    {
        Layers =
        [
            "Responsive ASP.NET Core MVC frontend for enterprise self-care journeys",
            "Customer OTP via SMS gateway plus CAM Active Directory / Azure AD access policy",
            "CRM profile lookup, BRM token retrieval, dashboard inventory query, and CRM service-instance enrichment",
            "BSS systems for CRM, billing, orders, payments and service fulfilment"
        ],
        IntegrationNotes =
        [
            "Customer mobile login now sends an OTP through the configured SMS API and verifies the code in-app.",
            "CRM remains the source for customer profile and service-instance account details.",
            "BRM token retrieval and dashboard inventory calls are used to fetch the service instance numbers before line enrichment."
        ]
    };

    public PortalDataService(
        IHttpClientFactory httpClientFactory,
        ILogger<PortalDataService> logger,
        IOptions<ActiveDirectoryOptions> activeDirectoryOptions,
        IOptions<SmsGatewayOptions> smsGatewayOptions,
        IOptions<CrmGatewayOptions> crmGatewayOptions,
        IOptions<BrmGatewayOptions> brmGatewayOptions)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _activeDirectoryOptions = activeDirectoryOptions.Value;
        _smsGatewayOptions = smsGatewayOptions.Value;
        _crmGatewayOptions = crmGatewayOptions.Value;
        _brmGatewayOptions = brmGatewayOptions.Value;
    }

    public async Task<PortalViewModel> GetPortalAsync(PortalCustomerSession? customerSession, string? bannerMessage = null, string bannerTone = "primary", string? assistantQuestion = null, string? assistantResponse = null, string? pendingOtpMobileNumber = null, bool hasPendingOtpChallenge = false, CancellationToken cancellationToken = default)
    {
        var accessSummary = BuildAccessSummary();
        var authentication = new PortalAuthenticationState
        {
            IsCustomerAuthenticated = customerSession is not null,
            AuthenticatedMobileNumber = customerSession?.MobileNumber,
            PendingOtpMobileNumber = pendingOtpMobileNumber,
            HasPendingOtpChallenge = hasPendingOtpChallenge
        };

        if (customerSession is null)
        {
            return CreatePortal(accessSummary, authentication, CreatePlaceholderAccount(), [], bannerMessage, bannerTone, assistantQuestion, assistantResponse);
        }

        try
        {
            var profile = await GetCustomerProfileAsync(customerSession.MobileNumber, cancellationToken);
            var lines = await GetLinesAsync(profile, cancellationToken);
            return CreatePortal(accessSummary, authentication, MapAccount(profile), lines, bannerMessage, bannerTone, assistantQuestion, assistantResponse);
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Unable to load portal data for the authenticated customer session.");
            var message = string.IsNullOrWhiteSpace(bannerMessage)
                ? exception.Message
                : $"{bannerMessage} {exception.Message}";
            return CreatePortal(accessSummary, authentication, CreatePlaceholderAccount(customerSession.MobileNumber), [], message, "warning", assistantQuestion, assistantResponse);
        }
    }

    public async Task<string> SendOtpAsync(string mobileNumber, CancellationToken cancellationToken = default)
    {
        var normalizedMobile = NormalizeMsisdn(mobileNumber);
        EnsureConfigured(_smsGatewayOptions.SendSmsUrl, "the SMS gateway URL");

        var otpCode = Random.Shared.Next(100000, 1000000).ToString(CultureInfo.InvariantCulture);
        var payload = new
        {
            originator = string.IsNullOrWhiteSpace(_smsGatewayOptions.Originator) ? "Emila" : _smsGatewayOptions.Originator,
            destination = normalizedMobile,
            messageContent = $"Your ONE Business Portal OTP is {otpCode}.",
            serviceId = _smsGatewayOptions.ServiceId
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, _smsGatewayOptions.SendSmsUrl)
        {
            Content = JsonContent.Create(payload)
        };

        try
        {
            using var response = await _httpClientFactory.CreateClient(PortalHttpClientName).SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"OTP delivery failed with HTTP {(int)response.StatusCode}.");
            }

            var result = await DeserializeAsync<SmsResponse>(response, cancellationToken);
            if (result?.ResultCode != 0)
            {
                throw new InvalidOperationException(result?.ResultMessage ?? "The OTP gateway rejected the request.");
            }
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException("The OTP gateway could not be reached.", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("The OTP gateway did not respond in time.", exception);
        }

        _pendingOtps[normalizedMobile] = new PendingOtpState(otpCode, DateTimeOffset.UtcNow.AddMinutes(5), 0);
        return $"OTP sent to {normalizedMobile}. Enter the verification code to continue.";
    }

    public Task<PortalCustomerSession> VerifyOtpAsync(string mobileNumber, string otpCode, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var normalizedMobile = NormalizeMsisdn(mobileNumber);
        var normalizedOtp = otpCode.Trim();

        if (!_pendingOtps.TryGetValue(normalizedMobile, out var pendingOtp))
        {
            throw new InvalidOperationException("No pending OTP was found for that mobile number. Please request a new code.");
        }

        if (pendingOtp.ExpiresAtUtc < DateTimeOffset.UtcNow)
        {
            _pendingOtps.TryRemove(normalizedMobile, out _);
            throw new InvalidOperationException("The OTP has expired. Please request a new code.");
        }

        if (!string.Equals(pendingOtp.Code, normalizedOtp, StringComparison.Ordinal))
        {
            var failedAttempts = pendingOtp.FailedAttempts + 1;
            if (failedAttempts >= MaxOtpAttempts)
            {
                _pendingOtps.TryRemove(normalizedMobile, out _);
                throw new InvalidOperationException("Too many invalid OTP attempts. Please request a new code.");
            }

            _pendingOtps[normalizedMobile] = pendingOtp with { FailedAttempts = failedAttempts };
            throw new InvalidOperationException("The OTP you entered is invalid.");
        }

        _pendingOtps.TryRemove(normalizedMobile, out _);
        return Task.FromResult(new PortalCustomerSession { MobileNumber = normalizedMobile });
    }

    public string ToggleFeature(FeatureUpdateRequest request)
    {
        var lineNumber = NormalizeMsisdn(request.LineNumber);
        var state = _lineFeatures.GetOrAdd(lineNumber, _ => CreateDefaultFeatureState());

        var updated = request.Feature switch
        {
            ManagedFeature.Roaming => state with { RoamingEnabled = !state.RoamingEnabled },
            ManagedFeature.InternationalCalls => state with { InternationalCallsEnabled = !state.InternationalCallsEnabled },
            ManagedFeature.Volte => state with { VolteEnabled = !state.VolteEnabled },
            _ => throw new InvalidOperationException("The requested feature is not supported.")
        };

        _lineFeatures[lineNumber] = updated;
        var featureName = request.Feature switch
        {
            ManagedFeature.Roaming => "Roaming",
            ManagedFeature.InternationalCalls => "International calls",
            ManagedFeature.Volte => "VoLTE",
            _ => "Feature"
        };

        var enabled = request.Feature switch
        {
            ManagedFeature.Roaming => updated.RoamingEnabled,
            ManagedFeature.InternationalCalls => updated.InternationalCallsEnabled,
            ManagedFeature.Volte => updated.VolteEnabled,
            _ => false
        };

        return $"{featureName} {(enabled ? "enabled" : "disabled")} for {lineNumber}.";
    }

    public string PurchaseAddOn(AddOnPurchaseRequest request)
    {
        var lineNumber = NormalizeMsisdn(request.LineNumber);
        var addOn = _addOns.FirstOrDefault(item => item.Code == request.AddOnCode)
            ?? throw new InvalidOperationException("The requested add-on is not available.");

        return $"{addOn.Name} requested for {lineNumber}. The purchase will be reflected in the current billing cycle.";
    }

    public string PayInvoice(PayInvoiceRequest request)
    {
        lock (_stateLock)
        {
            var invoice = _invoices.FirstOrDefault(item => item.InvoiceId == request.InvoiceId)
                ?? throw new InvalidOperationException("The invoice could not be found.");

            if (invoice.Status == "Paid")
            {
                return $"{invoice.InvoiceId} is already marked as paid.";
            }

            invoice.Status = "Paid";
            return $"Payment received for {invoice.InvoiceId}.";
        }
    }

    public InvoiceSummary? GetInvoice(string invoiceId)
    {
        var invoice = _invoices.FirstOrDefault(item => item.InvoiceId == invoiceId);
        return invoice is null ? null : Clone(invoice);
    }

    public string AskAssistant(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return "Ask about invoices, service activation or support tickets to get quick guidance.";
        }

        var normalized = question.Trim().ToLowerInvariant();

        if (normalized.Contains("invoice") || normalized.Contains("bill"))
        {
            return "Open the billing section to review invoice history, download the PDF statement, or use Pay Now for balances marked Due.";
        }

        if (normalized.Contains("roaming") || normalized.Contains("activate") || normalized.Contains("service"))
        {
            return "Use the line management table to toggle roaming, international calls, or VoLTE, and purchase one-off bundles from the add-on catalog.";
        }

        if (normalized.Contains("ticket") || normalized.Contains("complaint") || normalized.Contains("support"))
        {
            return "Track complaints in the ticket table and contact your dedicated CAM via phone, email or WhatsApp for escalations.";
        }

        return "I can help with service activation, invoices, payments and support tracking. Try asking about roaming, a due invoice or a support ticket.";
    }

    private CustomerAccessSummary BuildAccessSummary() => new()
    {
        CustomerLoginMethod = "Mobile number registration with OTP sent through the SMS API",
        StaffLoginMethod = "Corporate Active Directory access for CAM users via the configured domain",
        ActiveDirectoryDomain = _activeDirectoryOptions.Domain,
        AutoProvisionUsers = _activeDirectoryOptions.AutoProvisionUsers,
        Roles = new[] { "Enterprise Administrator", "Branch Manager", "CAM / Business Agent" }
    };

    private async Task<CustomerProfileContext> GetCustomerProfileAsync(string mobileNumber, CancellationToken cancellationToken)
    {
        EnsureConfigured(_crmGatewayOptions.WebServiceUrl, "the CRM web service URL");
        EnsureConfigured(_crmGatewayOptions.Username, "the CRM username");
        EnsureConfigured(_crmGatewayOptions.AccessKey, "the CRM access key");
        EnsureConfigured(_crmGatewayOptions.Source, "the CRM source value");

        var payload = new
        {
            operation = "get_customer_profile",
            username = _crmGatewayOptions.Username,
            accessKey = _crmGatewayOptions.AccessKey,
            source = _crmGatewayOptions.Source,
            customerData = new
            {
                type = "MSISDN",
                value = mobileNumber
            }
        };

        try
        {
            using var response = await _httpClientFactory.CreateClient(PortalHttpClientName).PostAsJsonAsync(_crmGatewayOptions.WebServiceUrl, payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"CRM profile lookup failed with HTTP {(int)response.StatusCode}.");
            }

            var profileResponse = await DeserializeAsync<CrmProfileResponse>(response, cancellationToken);
            if (profileResponse is null || !profileResponse.Success || profileResponse.Result?.Code != 0 || profileResponse.Result.CustomerDetails?.CustomerAccount is null)
            {
                throw new InvalidOperationException(profileResponse?.Result?.Message ?? "CRM profile lookup did not return customer details.");
            }

            return new CustomerProfileContext(profileResponse.Result.CustomerDetails);
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException("The CRM profile service could not be reached.", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("The CRM profile service did not respond in time.", exception);
        }
    }

    private async Task<IReadOnlyList<CorporateLine>> GetLinesAsync(CustomerProfileContext profile, CancellationToken cancellationToken)
    {
        var token = await GetDashboardTokenAsync(cancellationToken);
        var serviceInstances = await GetServiceInstanceNumbersAsync(profile.AccountNumber, token, cancellationToken);
        if (serviceInstances.Count == 0)
        {
            return Array.Empty<CorporateLine>();
        }

        var detailTasks = serviceInstances.Select(serviceInstanceNumber => GetServiceInstanceDetailAsync(serviceInstanceNumber, cancellationToken));
        var details = await Task.WhenAll(detailTasks);

        return details
            .Where(detail => detail is not null && !string.IsNullOrWhiteSpace(detail.ServiceinstanceMsisdn))
            .Select(detail => MapLine(profile, detail!))
            .ToArray();
    }

    private async Task<string> GetDashboardTokenAsync(CancellationToken cancellationToken)
    {
        EnsureConfigured(_brmGatewayOptions.AuthTokenUrl, "the BRM auth token URL");
        EnsureConfigured(_brmGatewayOptions.Username, "the BRM username");
        EnsureConfigured(_brmGatewayOptions.Password, "the BRM password");
        EnsureConfigured(_brmGatewayOptions.Source, "the BRM source value");

        var payload = new
        {
            ipAddress = string.IsNullOrWhiteSpace(_brmGatewayOptions.AuthIpAddress) ? "::" : _brmGatewayOptions.AuthIpAddress,
            source = _brmGatewayOptions.Source,
            referenceId = string.IsNullOrWhiteSpace(_brmGatewayOptions.AuthReferenceId) ? "123456" : _brmGatewayOptions.AuthReferenceId,
            username = _brmGatewayOptions.Username,
            password = _brmGatewayOptions.Password
        };

        try
        {
            using var response = await _httpClientFactory.CreateClient(PortalHttpClientName).PostAsJsonAsync(_brmGatewayOptions.AuthTokenUrl, payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"BRM token request failed with HTTP {(int)response.StatusCode}.");
            }

            var tokenResponse = await DeserializeAsync<BrmTokenResponse>(response, cancellationToken);
            var token = tokenResponse?.ResponseObject?.Token;
            if (tokenResponse?.ResponseCode != "0" || string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException(tokenResponse?.ResponseMessage ?? "The BRM token request did not return a usable token.");
            }

            return token;
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException("The BRM authorization service could not be reached.", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("The BRM authorization service did not respond in time.", exception);
        }
    }

    private async Task<IReadOnlyList<string>> GetServiceInstanceNumbersAsync(string customerAccountNumber, string token, CancellationToken cancellationToken)
    {
        EnsureConfigured(_brmGatewayOptions.DashboardUrl, "the BRM dashboard URL");
        EnsureConfigured(_brmGatewayOptions.DashboardIpAddress, "the BRM dashboard IP address");

        var payload = new
        {
            ipAddress = _brmGatewayOptions.DashboardIpAddress,
            source = _brmGatewayOptions.Source,
            referenceId = string.IsNullOrWhiteSpace(_brmGatewayOptions.DashboardReferenceId) ? "1234" : _brmGatewayOptions.DashboardReferenceId,
            customerAccountNumber,
            numberOfDisplaySI = _brmGatewayOptions.NumberOfDisplaySi,
            numberOfDispaySI = _brmGatewayOptions.NumberOfDisplaySi,
            fromDate = _brmGatewayOptions.FromDate,
            toDate = _brmGatewayOptions.ToDate
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, _brmGatewayOptions.DashboardUrl)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.TryAddWithoutValidation("Authorization", token);

        try
        {
            using var response = await _httpClientFactory.CreateClient(PortalHttpClientName).SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"BRM dashboard lookup failed with HTTP {(int)response.StatusCode}.");
            }

            var dashboardResponse = await DeserializeAsync<BrmDashboardResponse>(response, cancellationToken);
            if (dashboardResponse?.ResponseCode != "0")
            {
                throw new InvalidOperationException(dashboardResponse?.ResponseMessage ?? "The BRM dashboard lookup did not succeed.");
            }

            return dashboardResponse.ResponseObject?.ServiceInstanceDetailsList?
                .Where(item => !string.IsNullOrWhiteSpace(item.SiNo))
                .Select(item => item.SiNo!)
                .Distinct(StringComparer.Ordinal)
                .ToArray() ?? Array.Empty<string>();
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException("The BRM dashboard service could not be reached.", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("The BRM dashboard service did not respond in time.", exception);
        }
    }

    private async Task<ServiceInstanceRecord?> GetServiceInstanceDetailAsync(string serviceInstanceNumber, CancellationToken cancellationToken)
    {
        EnsureSafeServiceInstanceNumber(serviceInstanceNumber);
        var query = BuildServiceInstanceQuery(serviceInstanceNumber);
        var payload = new
        {
            operation = "query",
            username = _crmGatewayOptions.Username,
            accessKey = _crmGatewayOptions.AccessKey,
            query
        };

        try
        {
            using var response = await _httpClientFactory.CreateClient(PortalHttpClientName).PostAsJsonAsync(_crmGatewayOptions.WebServiceUrl, payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"CRM service-instance query failed with HTTP {(int)response.StatusCode} for {serviceInstanceNumber}.");
            }

            var queryResponse = await DeserializeAsync<CrmQueryResponse>(response, cancellationToken);
            if (queryResponse is null || !queryResponse.Success)
            {
                throw new InvalidOperationException($"CRM service-instance query failed for {serviceInstanceNumber}.");
            }

            return queryResponse.Result?.FirstOrDefault();
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException("The CRM service-instance lookup could not be reached.", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("The CRM service-instance lookup did not respond in time.", exception);
        }
    }

    private PortalViewModel CreatePortal(CustomerAccessSummary accessSummary, PortalAuthenticationState authentication, AccountOverview account, IReadOnlyList<CorporateLine> lines, string? bannerMessage, string bannerTone, string? assistantQuestion, string? assistantResponse)
    {
        var metrics = new[]
        {
            CreateMetric("Voice & data", lines.Count(line => line.LineType.Contains("voice", StringComparison.OrdinalIgnoreCase) || line.LineType.Contains("postpaid", StringComparison.OrdinalIgnoreCase)), "border-primary"),
            CreateMetric("Data-only SIM", lines.Count(line => line.LineType.Contains("data", StringComparison.OrdinalIgnoreCase) && !line.LineType.Contains("voice", StringComparison.OrdinalIgnoreCase)), "border-success"),
            CreateMetric("IoT SIM", lines.Count(line => line.LineType.Contains("iot", StringComparison.OrdinalIgnoreCase)), "border-info"),
            CreateMetric("Active / inactive / pending", $"{lines.Count(line => line.ServiceStatus == "Active")} / {lines.Count(line => line.ServiceStatus == "Inactive")} / {lines.Count(line => line.ServiceStatus == "Pending")}", "border-warning")
        };

        return new PortalViewModel
        {
            AccessSummary = accessSummary,
            Authentication = authentication,
            Account = account,
            LineMetrics = metrics,
            Lines = lines,
            AddOnCatalog = _addOns.Select(Clone).ToArray(),
            Tariffs = _tariffs.Select(Clone).ToArray(),
            Invoices = _invoices.Select(Clone).ToArray(),
            Tickets = _tickets.Select(Clone).ToArray(),
            SupportContact = Clone(_supportContact),
            Offers = _offers.Select(Clone).ToArray(),
            Architecture = Clone(_architecture),
            AssistantQuestion = assistantQuestion,
            AssistantResponse = assistantResponse,
            BannerMessage = bannerMessage,
            BannerTone = bannerTone
        };
    }

    private AccountOverview MapAccount(CustomerProfileContext profile)
    {
        var account = profile.CustomerDetails.CustomerAccount!;
        var createdDate = ParseDate(account.CreatedDate, DateOnly.FromDateTime(DateTime.UtcNow.Date));
        var endDate = createdDate.AddDays(Math.Max(account.NetworkAge, 0));
        var monthlyBudget = decimal.TryParse(account.CreditLimit, NumberStyles.Any, CultureInfo.InvariantCulture, out var budget)
            ? budget
            : 0m;

        return new AccountOverview
        {
            CompanyName = FirstNonEmpty(account.AccountName, profile.CustomerDetails.CustomerContact?.Name, "Authenticated customer"),
            CustomerCode = FirstNonEmpty(account.AccountNumber, "Unavailable"),
            TaxId = FirstNonEmpty(account.VatTaxNumber, account.Nid, "Unavailable"),
            ContractDuration = account.NetworkAge > 0 ? $"{account.NetworkAge} days network age" : "Not provided by CRM",
            ContractStartDate = createdDate,
            ContractEndDate = endDate,
            MonthlyBudget = monthlyBudget,
            CorporateDiscountPercentage = 0
        };
    }

    private CorporateLine MapLine(CustomerProfileContext profile, ServiceInstanceRecord detail)
    {
        var lineNumber = NormalizeMsisdn(detail.ServiceinstanceMsisdn ?? detail.Serviceinstnum ?? string.Empty);
        var status = string.Equals(detail.ServiceinstanceActStatus, "1", StringComparison.Ordinal)
            ? "Active"
            : "Inactive";
        var featureState = _lineFeatures.GetOrAdd(lineNumber, _ => CreateDefaultFeatureState(status));

        return new CorporateLine
        {
            Number = lineNumber,
            Owner = FirstNonEmpty(profile.CustomerDetails.CustomerContact?.Name, profile.CustomerDetails.CustomerAccount?.AccountName, "Corporate line"),
            Branch = FirstNonEmpty(profile.CustomerDetails.CustomerAddress?.City, profile.CustomerDetails.CustomerAddress?.District, "CRM account"),
            LineType = FirstNonEmpty(detail.SiInstanceServiceType, "Corporate"),
            Package = FirstNonEmpty(detail.Serviceinstnum, detail.SrinaccPackname, "Service instance"),
            ServiceStatus = status,
            Usage = BuildUsage(detail.StartDate),
            RoamingEnabled = featureState.RoamingEnabled,
            InternationalCallsEnabled = featureState.InternationalCallsEnabled,
            VolteEnabled = featureState.VolteEnabled
        };
    }

    private static string BuildUsage(string? startDate)
    {
        if (DateTime.TryParse(startDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedDate))
        {
            return $"Activated {parsedDate:dd MMM yyyy}";
        }

        return "Activation date unavailable";
    }

    private static AccountOverview CreatePlaceholderAccount(string? mobileNumber = null) => new()
    {
        CompanyName = mobileNumber is null ? "Sign in to load CRM account data" : $"Portal session for {mobileNumber}",
        CustomerCode = "Unavailable",
        TaxId = "Unavailable",
        ContractDuration = "Unavailable until CRM profile is loaded",
        ContractStartDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
        ContractEndDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
        MonthlyBudget = 0m,
        CorporateDiscountPercentage = 0
    };

    private static MetricCard CreateMetric(string label, object value, string accentClass) => new()
    {
        Label = label,
        Value = value.ToString() ?? string.Empty,
        AccentClass = accentClass
    };

    private static AddOnOption Clone(AddOnOption addOn) => new()
    {
        Code = addOn.Code,
        Name = addOn.Name,
        Description = addOn.Description,
        Price = addOn.Price
    };

    private static TariffPackage Clone(TariffPackage tariff) => new()
    {
        Name = tariff.Name,
        Description = tariff.Description,
        BillingCycle = tariff.BillingCycle,
        Eligibility = tariff.Eligibility
    };

    private static InvoiceSummary Clone(InvoiceSummary invoice) => new()
    {
        InvoiceId = invoice.InvoiceId,
        BillingPeriod = invoice.BillingPeriod,
        TotalAmount = invoice.TotalAmount,
        Status = invoice.Status,
        DueDate = invoice.DueDate,
        StatementLines = invoice.StatementLines.ToArray()
    };

    private static SupportTicket Clone(SupportTicket ticket) => new()
    {
        TicketId = ticket.TicketId,
        Subject = ticket.Subject,
        Priority = ticket.Priority,
        Status = ticket.Status,
        OpenedOn = ticket.OpenedOn
    };

    private static SupportContact Clone(SupportContact contact) => new()
    {
        Name = contact.Name,
        Phone = contact.Phone,
        Email = contact.Email,
        WhatsAppLink = contact.WhatsAppLink
    };

    private static OfferRecommendation Clone(OfferRecommendation offer) => new()
    {
        Title = offer.Title,
        Summary = offer.Summary,
        Segment = offer.Segment,
        Trigger = offer.Trigger
    };

    private static ArchitectureSummary Clone(ArchitectureSummary architecture) => new()
    {
        Layers = architecture.Layers.ToArray(),
        IntegrationNotes = architecture.IntegrationNotes.ToArray()
    };

    private static string NormalizeMsisdn(string value) => new(value.Where(char.IsDigit).ToArray());

    private static DateOnly ParseDate(string? value, DateOnly fallback)
    {
        if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly))
        {
            return dateOnly;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dateTime))
        {
            return DateOnly.FromDateTime(dateTime);
        }

        return fallback;
    }

    private static string FirstNonEmpty(params string?[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static FeatureState CreateDefaultFeatureState(string status = "Active") => new(
        RoamingEnabled: string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase),
        InternationalCallsEnabled: string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase),
        VolteEnabled: true);

    private static void EnsureConfigured(string value, string settingName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Portal integration cannot run until {settingName} is configured.");
        }
    }

    private static void EnsureSafeServiceInstanceNumber(string serviceInstanceNumber)
    {
        if (string.IsNullOrWhiteSpace(serviceInstanceNumber) || serviceInstanceNumber.Any(character => !char.IsLetterOrDigit(character)))
        {
            throw new InvalidOperationException("An invalid service instance number was returned by the upstream system.");
        }
    }

    private static string BuildServiceInstanceQuery(string serviceInstanceNumber)
    {
        // The CRM endpoint accepts only a textual query payload, so the service instance value is validated
        // as strictly alphanumeric before it is embedded in the statement.
        return $"SELECT * FROM ServiceInstanceAccount WHERE serviceinstnum = '{serviceInstanceNumber}';";
    }

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, SerializerOptions, cancellationToken);
    }

    private sealed record PendingOtpState(string Code, DateTimeOffset ExpiresAtUtc, int FailedAttempts);

    private sealed record FeatureState(bool RoamingEnabled, bool InternationalCallsEnabled, bool VolteEnabled);

    private sealed record CustomerProfileContext(CrmCustomerDetails CustomerDetails)
    {
        public string AccountNumber => CustomerDetails.CustomerAccount?.AccountNumber ?? string.Empty;
    }

    private sealed class SmsResponse
    {
        public int ResultCode { get; init; }
        public string? ResultMessage { get; init; }
    }

    private sealed class CrmProfileResponse
    {
        public bool Success { get; init; }
        public CrmProfileResult? Result { get; init; }
    }

    private sealed class CrmProfileResult
    {
        public int Code { get; init; }
        public string? Message { get; init; }
        public CrmCustomerDetails? CustomerDetails { get; init; }
    }

    private sealed class CrmCustomerDetails
    {
        public CrmCustomerAccount? CustomerAccount { get; init; }
        public CrmCustomerContact? CustomerContact { get; init; }
        public CrmCustomerAddress? CustomerAddress { get; init; }
    }

    private sealed class CrmCustomerAccount
    {
        public string? AccountNumber { get; init; }
        public string? AccountName { get; init; }
        public string? VatTaxNumber { get; init; }
        public string? Nid { get; init; }
        public string? CreditLimit { get; init; }
        public int NetworkAge { get; init; }
        [JsonPropertyName("created_date")]
        public string? CreatedDate { get; init; }
    }

    private sealed class CrmCustomerContact
    {
        public string? Name { get; init; }
        public string? Phone { get; init; }
        public string? Email { get; init; }
    }

    private sealed class CrmCustomerAddress
    {
        public string? District { get; init; }
        public string? City { get; init; }
    }

    private sealed class BrmTokenResponse
    {
        public string? ResponseCode { get; init; }
        public string? ResponseMessage { get; init; }
        public BrmTokenObject? ResponseObject { get; init; }
    }

    private sealed class BrmTokenObject
    {
        public string? Token { get; init; }
    }

    private sealed class BrmDashboardResponse
    {
        public string? ResponseCode { get; init; }
        public string? ResponseMessage { get; init; }
        public BrmDashboardObject? ResponseObject { get; init; }
    }

    private sealed class BrmDashboardObject
    {
        [JsonPropertyName("serviceInstanceDetailsList")]
        public IReadOnlyList<BrmServiceInstance>? ServiceInstanceDetailsList { get; init; }
    }

    private sealed class BrmServiceInstance
    {
        public string? SiNo { get; init; }
    }

    private sealed class CrmQueryResponse
    {
        public bool Success { get; init; }
        public IReadOnlyList<ServiceInstanceRecord>? Result { get; init; }
    }

    private sealed class ServiceInstanceRecord
    {
        public string? Serviceinstnum { get; init; }
        [JsonPropertyName("serviceinstance_msisdn")]
        public string? ServiceinstanceMsisdn { get; init; }
        [JsonPropertyName("serviceinstance_act_status")]
        public string? ServiceinstanceActStatus { get; init; }
        [JsonPropertyName("si_instance_service_type")]
        public string? SiInstanceServiceType { get; init; }
        [JsonPropertyName("srinacc_packname")]
        public string? SrinaccPackname { get; init; }
        [JsonPropertyName("start_date")]
        public string? StartDate { get; init; }
    }
}
