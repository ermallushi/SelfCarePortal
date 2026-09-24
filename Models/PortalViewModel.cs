using System.ComponentModel.DataAnnotations;

namespace SelfCarePortal.Models;

public class PortalViewModel
{
    public required CustomerAccessSummary AccessSummary { get; init; }
    public required PortalAuthenticationState Authentication { get; init; }
    public required AccountOverview Account { get; init; }
    public required IReadOnlyList<MetricCard> LineMetrics { get; init; }
    public required IReadOnlyList<CorporateLine> Lines { get; init; }
    public required IReadOnlyList<AddOnOption> AddOnCatalog { get; init; }
    public required IReadOnlyList<TariffPackage> Tariffs { get; init; }
    public required IReadOnlyList<InvoiceSummary> Invoices { get; init; }
    public required IReadOnlyList<SupportTicket> Tickets { get; init; }
    public required SupportContact SupportContact { get; init; }
    public required IReadOnlyList<OfferRecommendation> Offers { get; init; }
    public required ArchitectureSummary Architecture { get; init; }
    public string? AssistantQuestion { get; init; }
    public string? AssistantResponse { get; init; }
    public string? BannerMessage { get; init; }
    public string BannerTone { get; init; } = "primary";
}

public class PortalAuthenticationState
{
    public bool IsCustomerAuthenticated { get; init; }
    public string? AuthenticatedMobileNumber { get; init; }
    public string? PendingOtpMobileNumber { get; init; }
    public bool HasPendingOtpChallenge { get; init; }
}

public class CustomerAccessSummary
{
    public required string CustomerLoginMethod { get; init; }
    public required string StaffLoginMethod { get; init; }
    public required string ActiveDirectoryDomain { get; init; }
    public bool AutoProvisionUsers { get; init; }
    public required IReadOnlyList<string> Roles { get; init; }
}

public class AccountOverview
{
    public required string CompanyName { get; init; }
    public required string CustomerCode { get; init; }
    public required string TaxId { get; init; }
    public required string ContractDuration { get; init; }
    public required DateOnly ContractStartDate { get; init; }
    public required DateOnly ContractEndDate { get; init; }
    public required decimal MonthlyBudget { get; init; }
    public required int CorporateDiscountPercentage { get; init; }
}

public class MetricCard
{
    public required string Label { get; init; }
    public required string Value { get; init; }
    public required string AccentClass { get; init; }
}

public class CorporateLine
{
    public required string Number { get; set; }
    public required string Owner { get; init; }
    public required string Branch { get; init; }
    public required string LineType { get; init; }
    public required string Package { get; init; }
    public required string ServiceStatus { get; set; }
    public required string Usage { get; init; }
    public bool RoamingEnabled { get; set; }
    public bool InternationalCallsEnabled { get; set; }
    public bool VolteEnabled { get; set; }
}

public class AddOnOption
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Price { get; init; }
}

public class TariffPackage
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string BillingCycle { get; init; }
    public required string Eligibility { get; init; }
}

public class InvoiceSummary
{
    public required string InvoiceId { get; set; }
    public required string BillingPeriod { get; init; }
    public required decimal TotalAmount { get; init; }
    public required string Status { get; set; }
    public required DateOnly DueDate { get; init; }
    public required IReadOnlyList<string> StatementLines { get; init; }
}

public class SupportTicket
{
    public required string TicketId { get; init; }
    public required string Subject { get; init; }
    public required string Priority { get; init; }
    public required string Status { get; init; }
    public required DateOnly OpenedOn { get; init; }
}

public class SupportContact
{
    public required string Name { get; init; }
    public required string Phone { get; init; }
    public required string Email { get; init; }
    public required string WhatsAppLink { get; init; }
}

public class OfferRecommendation
{
    public required string Title { get; init; }
    public required string Summary { get; init; }
    public required string Segment { get; init; }
    public required string Trigger { get; init; }
}

public class ArchitectureSummary
{
    public required IReadOnlyList<string> Layers { get; init; }
    public required IReadOnlyList<string> IntegrationNotes { get; init; }
}

public class PortalCustomerSession
{
    public required string MobileNumber { get; init; }
}

public enum ManagedFeature
{
    Roaming,
    InternationalCalls,
    Volte
}

public class SendOtpRequest
{
    [Required]
    public string MobileNumber { get; init; } = string.Empty;
}

public class VerifyOtpRequest
{
    [Required]
    public string MobileNumber { get; init; } = string.Empty;

    [Required]
    public string OtpCode { get; init; } = string.Empty;
}

public class FeatureUpdateRequest
{
    [Required]
    public string LineNumber { get; init; } = string.Empty;

    [Required]
    public ManagedFeature Feature { get; init; }
}

public class AddOnPurchaseRequest
{
    [Required]
    public string LineNumber { get; init; } = string.Empty;

    [Required]
    public string AddOnCode { get; init; } = string.Empty;
}

public class PayInvoiceRequest
{
    [Required]
    public string InvoiceId { get; init; } = string.Empty;
}

public class AssistantPromptRequest
{
    [Required]
    public string Question { get; init; } = string.Empty;
}
