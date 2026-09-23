using SelfCarePortal.Models;

namespace SelfCarePortal.Services;

public interface IPortalDataService
{
    PortalViewModel GetPortal(string? bannerMessage = null, string bannerTone = "primary", string? assistantQuestion = null, string? assistantResponse = null);
    string ToggleFeature(FeatureUpdateRequest request);
    string PurchaseAddOn(AddOnPurchaseRequest request);
    string PayInvoice(PayInvoiceRequest request);
    InvoiceSummary? GetInvoice(string invoiceId);
    string AskAssistant(string question);
}
