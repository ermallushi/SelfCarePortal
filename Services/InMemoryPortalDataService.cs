using SelfCarePortal.Models;

namespace SelfCarePortal.Services;

public class InMemoryPortalDataService : IPortalDataService
{
    private readonly object _syncRoot = new();

    private readonly AccountOverview _account = new()
    {
        CompanyName = "AlbBank Corporate",
        CustomerCode = "ALB-BANK-00214",
        TaxId = "L71412019A",
        ContractDuration = "36 months",
        ContractStartDate = new DateOnly(2025, 1, 1),
        ContractEndDate = new DateOnly(2027, 12, 31),
        MonthlyBudget = 12500m,
        CorporateDiscountPercentage = 18
    };

    private readonly CustomerAccessSummary _access = new()
    {
        CustomerLoginMethod = "Mobile number registration with OTP / Mobile Connect",
        StaffLoginMethod = "Azure Active Directory single sign-on for CAM and internal staff",
        Roles = new[] { "Enterprise Administrator", "Branch Manager", "CAM / Business Agent" }
    };

    private readonly List<CorporateLine> _lines =
    [
        new()
        {
            Number = "+355 69 201 1401",
            Owner = "Treasury Desk",
            Branch = "HQ",
            LineType = "Voice & Data",
            Package = "Business Max 50GB",
            ServiceStatus = "Active",
            Usage = "41GB / 50GB",
            RoamingEnabled = true,
            InternationalCallsEnabled = true,
            VolteEnabled = true
        },
        new()
        {
            Number = "+355 69 201 1402",
            Owner = "Branch Operations",
            Branch = "Durres",
            LineType = "Data-only SIM",
            Package = "Data Share 100GB",
            ServiceStatus = "Pending",
            Usage = "12GB / 100GB",
            RoamingEnabled = false,
            InternationalCallsEnabled = false,
            VolteEnabled = false
        },
        new()
        {
            Number = "+355 69 201 1403",
            Owner = "ATM Fleet",
            Branch = "IoT Network",
            LineType = "IoT SIM",
            Package = "IoT Secure 2GB",
            ServiceStatus = "Inactive",
            Usage = "400MB / 2GB",
            RoamingEnabled = false,
            InternationalCallsEnabled = false,
            VolteEnabled = true
        }
    ];

    private readonly List<AddOnOption> _addOns =
    [
        new() { Code = "ROAM-7", Name = "Roaming Saver", Description = "7-day regional roaming bundle", Price = "ALL 1,500" },
        new() { Code = "DATA-20", Name = "Extra Data 20GB", Description = "One-off data boost for the current billing cycle", Price = "ALL 2,200" },
        new() { Code = "INT-200", Name = "International 200", Description = "200 international minutes for priority destinations", Price = "ALL 1,900" }
    ];

    private readonly List<TariffPackage> _tariffs =
    [
        new() { Name = "Business Max 50GB", Description = "Voice, data and roaming-ready package for managers", BillingCycle = "Monthly", Eligibility = "Enterprise administrators & CAM-approved users" },
        new() { Name = "Data Share 100GB", Description = "High-capacity data-only bundle for tablets and routers", BillingCycle = "Monthly", Eligibility = "Branch managers for data-only SIM estate" },
        new() { Name = "IoT Secure 2GB", Description = "Telemetry package with device monitoring readiness", BillingCycle = "Monthly", Eligibility = "IoT estate and ATM fleet devices" }
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
        new() { Title = "Roaming Saver", Summary = "Recommended for executives traveling twice this month.", Segment = "Frequent travel", Trigger = "High outbound roaming usage" },
        new() { Title = "Data Boost", Summary = "Increase branch data pool ahead of quarter-end reporting.", Segment = "Branch operations", Trigger = "80% package utilization reached" },
        new() { Title = "Upgrade Bundle", Summary = "Move the ATM fleet to the secure IoT package refresh.", Segment = "IoT devices", Trigger = "Legacy package renewal window" }
    ];

    private readonly ArchitectureSummary _architecture = new()
    {
        Layers =
        [
            "Responsive ASP.NET Core MVC frontend for enterprise self-care journeys",
            "API gateway and authentication layer for OTP-based customer access plus Azure AD SSO for staff",
            "Reusable digital/TMF/B2B APIs and CMS contextual campaign integration",
            "BSS systems for CRM, billing, orders, payments and service fulfilment"
        ],
        IntegrationNotes =
        [
            "BSS remains the source of truth for eligibility, billing, order and payment rules.",
            "CMS drives message presentation and campaign targeting without duplicating core business logic.",
            "CAM and internal roles are separated through RBAC-ready enterprise profiles."
        ]
    };

    public PortalViewModel GetPortal(string? bannerMessage = null, string bannerTone = "primary", string? assistantQuestion = null, string? assistantResponse = null)
    {
        lock (_syncRoot)
        {
            var metrics = new[]
            {
                CreateMetric("Voice & data", _lines.Count(line => line.LineType == "Voice & Data"), "border-primary"),
                CreateMetric("Data-only SIM", _lines.Count(line => line.LineType == "Data-only SIM"), "border-success"),
                CreateMetric("IoT SIM", _lines.Count(line => line.LineType == "IoT SIM"), "border-info"),
                CreateMetric("Active / inactive / pending", $"{_lines.Count(line => line.ServiceStatus == "Active")} / {_lines.Count(line => line.ServiceStatus == "Inactive")} / {_lines.Count(line => line.ServiceStatus == "Pending")}", "border-warning")
            };

            return new PortalViewModel
            {
                AccessSummary = _access,
                Account = _account,
                LineMetrics = metrics,
                Lines = _lines.Select(Clone).ToArray(),
                AddOnCatalog = _addOns.ToArray(),
                Tariffs = _tariffs.ToArray(),
                Invoices = _invoices.Select(Clone).ToArray(),
                Tickets = _tickets.ToArray(),
                SupportContact = _supportContact,
                Offers = _offers.ToArray(),
                Architecture = _architecture,
                AssistantQuestion = assistantQuestion,
                AssistantResponse = assistantResponse,
                BannerMessage = bannerMessage,
                BannerTone = bannerTone
            };
        }
    }

    public string ToggleFeature(FeatureUpdateRequest request)
    {
        lock (_syncRoot)
        {
            var line = _lines.FirstOrDefault(item => item.Number == request.LineNumber)
                ?? throw new InvalidOperationException("The selected line could not be found.");

            var (featureName, enabled) = request.Feature switch
            {
                ManagedFeature.Roaming => ("Roaming", line.RoamingEnabled = !line.RoamingEnabled),
                ManagedFeature.InternationalCalls => ("International calls", line.InternationalCallsEnabled = !line.InternationalCallsEnabled),
                ManagedFeature.Volte => ("VoLTE", line.VolteEnabled = !line.VolteEnabled),
                _ => throw new InvalidOperationException("The requested feature is not supported.")
            };

            return $"{featureName} {(enabled ? "enabled" : "disabled")} for {line.Number}.";
        }
    }

    public string PurchaseAddOn(AddOnPurchaseRequest request)
    {
        lock (_syncRoot)
        {
            var line = _lines.FirstOrDefault(item => item.Number == request.LineNumber)
                ?? throw new InvalidOperationException("The selected line could not be found.");
            var addOn = _addOns.FirstOrDefault(item => item.Code == request.AddOnCode)
                ?? throw new InvalidOperationException("The requested add-on is not available.");

            return $"{addOn.Name} requested for {line.Number}. The purchase will be reflected in the current billing cycle.";
        }
    }

    public string PayInvoice(PayInvoiceRequest request)
    {
        lock (_syncRoot)
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
        lock (_syncRoot)
        {
            var invoice = _invoices.FirstOrDefault(item => item.InvoiceId == invoiceId);
            return invoice is null ? null : Clone(invoice);
        }
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

    private static MetricCard CreateMetric(string label, object value, string accentClass) => new()
    {
        Label = label,
        Value = value.ToString() ?? string.Empty,
        AccentClass = accentClass
    };

    private static CorporateLine Clone(CorporateLine line) => new()
    {
        Number = line.Number,
        Owner = line.Owner,
        Branch = line.Branch,
        LineType = line.LineType,
        Package = line.Package,
        ServiceStatus = line.ServiceStatus,
        Usage = line.Usage,
        RoamingEnabled = line.RoamingEnabled,
        InternationalCallsEnabled = line.InternationalCallsEnabled,
        VolteEnabled = line.VolteEnabled
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
}
