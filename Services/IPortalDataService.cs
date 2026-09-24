using SelfCarePortal.Models;

namespace SelfCarePortal.Services;

public interface IPortalDataService
{
    Task<PortalViewModel> GetPortalAsync(PortalCustomerSession? customerSession, string? bannerMessage = null, string bannerTone = "primary", string? assistantQuestion = null, string? assistantResponse = null, string? pendingOtpMobileNumber = null, bool hasPendingOtpChallenge = false, CancellationToken cancellationToken = default);
    Task<string> SendOtpAsync(string mobileNumber, CancellationToken cancellationToken = default);
    Task<PortalCustomerSession> VerifyOtpAsync(string mobileNumber, string otpCode, CancellationToken cancellationToken = default);
    string ToggleFeature(FeatureUpdateRequest request);
    string PurchaseAddOn(AddOnPurchaseRequest request);
    string PayInvoice(PayInvoiceRequest request);
    InvoiceSummary? GetInvoice(string invoiceId);
    string AskAssistant(string question);
}
