using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SelfCarePortal.Models;
using SelfCarePortal.Services;

namespace SelfCarePortal.Controllers;

public class HomeController : Controller
{
    private const string CustomerSessionKey = "PortalCustomerSession";
    private readonly ILogger<HomeController> _logger;
    private readonly IPortalDataService _portalDataService;

    public HomeController(ILogger<HomeController> logger, IPortalDataService portalDataService)
    {
        _logger = logger;
        _portalDataService = portalDataService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await BuildPortalViewAsync(cancellationToken: cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendOtp(SendOtpRequest request, CancellationToken cancellationToken)
    {
        var mobileNumber = NormalizeMsisdn(request.MobileNumber);
        if (string.IsNullOrWhiteSpace(mobileNumber))
        {
            return View("Index", await BuildPortalViewAsync("Enter a valid mobile number to request an OTP.", "danger", pendingOtpMobileNumber: request.MobileNumber, hasPendingOtpChallenge: false, cancellationToken: cancellationToken));
        }

        try
        {
            var message = await _portalDataService.SendOtpAsync(mobileNumber, cancellationToken);
            return View("Index", await BuildPortalViewAsync(message, "info", pendingOtpMobileNumber: mobileNumber, hasPendingOtpChallenge: true, cancellationToken: cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "OTP request failed for mobile number {MobileNumber}", mobileNumber);
            return View("Index", await BuildPortalViewAsync(exception.Message, "danger", pendingOtpMobileNumber: mobileNumber, hasPendingOtpChallenge: false, cancellationToken: cancellationToken));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyOtp(VerifyOtpRequest request, CancellationToken cancellationToken)
    {
        var mobileNumber = NormalizeMsisdn(request.MobileNumber);
        if (string.IsNullOrWhiteSpace(mobileNumber) || string.IsNullOrWhiteSpace(request.OtpCode))
        {
            return View("Index", await BuildPortalViewAsync("Enter both the mobile number and the OTP code.", "danger", pendingOtpMobileNumber: mobileNumber, hasPendingOtpChallenge: true, cancellationToken: cancellationToken));
        }

        try
        {
            var session = await _portalDataService.VerifyOtpAsync(mobileNumber, request.OtpCode, cancellationToken);
            SaveCustomerSession(session);
            return View("Index", await BuildPortalViewAsync("Customer login successful.", "success", cancellationToken: cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "OTP verification failed for mobile number {MobileNumber}", mobileNumber);
            return View("Index", await BuildPortalViewAsync(exception.Message, "danger", pendingOtpMobileNumber: mobileNumber, hasPendingOtpChallenge: true, cancellationToken: cancellationToken));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        HttpContext.Session.Remove(CustomerSessionKey);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleFeature(FeatureUpdateRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", await BuildPortalViewAsync("The feature update request is invalid.", "danger", cancellationToken: cancellationToken));
        }

        return await RenderPortalOperationAsync(() => _portalDataService.ToggleFeature(request), cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PurchaseAddOn(AddOnPurchaseRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", await BuildPortalViewAsync("The add-on purchase request is invalid.", "danger", cancellationToken: cancellationToken));
        }

        return await RenderPortalOperationAsync(() => _portalDataService.PurchaseAddOn(request), cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PayInvoice(PayInvoiceRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", await BuildPortalViewAsync("The payment request is invalid.", "danger", cancellationToken: cancellationToken));
        }

        return await RenderPortalOperationAsync(() => _portalDataService.PayInvoice(request), cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AskAssistant(AssistantPromptRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", await BuildPortalViewAsync("Please enter a question for the assistant.", "danger", cancellationToken: cancellationToken));
        }

        var response = _portalDataService.AskAssistant(request.Question);
        return View("Index", await BuildPortalViewAsync("Assistant response generated.", "info", request.Question, response, cancellationToken: cancellationToken));
    }

    public IActionResult InvoiceDetails(string id)
    {
        var invoice = _portalDataService.GetInvoice(id);
        return invoice is null ? NotFound() : View(invoice);
    }

    public IActionResult InvoicePdf(string id)
    {
        var invoice = _portalDataService.GetInvoice(id);
        if (invoice is null)
        {
            return NotFound();
        }

        var bytes = BuildInvoicePdf(invoice);
        return File(bytes, "application/pdf", $"{invoice.InvoiceId}.pdf");
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    private async Task<PortalViewModel> BuildPortalViewAsync(string? bannerMessage = null, string bannerTone = "primary", string? assistantQuestion = null, string? assistantResponse = null, string? pendingOtpMobileNumber = null, bool hasPendingOtpChallenge = false, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _portalDataService.GetPortalAsync(GetCustomerSession(), bannerMessage, bannerTone, assistantQuestion, assistantResponse, pendingOtpMobileNumber, hasPendingOtpChallenge, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Portal view loading failed.");
            return await _portalDataService.GetPortalAsync(null, exception.Message, "danger", assistantQuestion, assistantResponse, pendingOtpMobileNumber, hasPendingOtpChallenge, cancellationToken);
        }
    }

    private async Task<IActionResult> RenderPortalOperationAsync(Func<string> operation, CancellationToken cancellationToken)
    {
        try
        {
            var message = operation();
            return View("Index", await BuildPortalViewAsync(message, "success", cancellationToken: cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Portal operation failed.");
            return View("Index", await BuildPortalViewAsync(exception.Message, "danger", cancellationToken: cancellationToken));
        }
    }

    private PortalCustomerSession? GetCustomerSession()
    {
        var json = HttpContext.Session.GetString(CustomerSessionKey);
        return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<PortalCustomerSession>(json);
    }

    private void SaveCustomerSession(PortalCustomerSession session)
    {
        HttpContext.Session.SetString(CustomerSessionKey, JsonSerializer.Serialize(session));
    }

    private static string NormalizeMsisdn(string value) => new(value.Where(char.IsDigit).ToArray());

    private static byte[] BuildInvoicePdf(InvoiceSummary invoice)
    {
        var lines = new List<string>
        {
            $"Invoice {invoice.InvoiceId}",
            $"Billing period: {invoice.BillingPeriod}",
            $"Status: {invoice.Status}",
            $"Due date: {invoice.DueDate:dd MMM yyyy}",
            $"Total amount: ALL {invoice.TotalAmount:N0}"
        };
        lines.AddRange(invoice.StatementLines);

        static string Escape(string value) => value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        static string SanitizePdfText(string value) => new(value.Select(character => character <= 127 ? character : '?').ToArray());

        var content = new StringBuilder();
        content.AppendLine("BT");
        content.AppendLine("/F1 16 Tf");
        content.AppendLine("72 740 Td");

        for (var index = 0; index < lines.Count; index++)
        {
            if (index > 0)
            {
                content.AppendLine("0 -24 Td");
            }

            content.Append('(').Append(Escape(SanitizePdfText(lines[index]))).AppendLine(") Tj");
        }

        content.AppendLine("ET");
        var stream = content.ToString();

        var objects = new List<string>();
        objects.Add("1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj\n");
        objects.Add("2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj\n");
        objects.Add("3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >> endobj\n");
        objects.Add($"4 0 obj << /Length {Encoding.ASCII.GetByteCount(stream)} >> stream\n{stream}endstream\nendobj\n");
        objects.Add("5 0 obj << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> endobj\n");

        const string header = "%PDF-1.4\n";
        var pdf = new StringBuilder(header);

        var offsets = new List<int>();
        var runningLength = Encoding.ASCII.GetByteCount(header);
        foreach (var pdfObject in objects)
        {
            offsets.Add(runningLength);
            pdf.Append(pdfObject);
            runningLength += Encoding.ASCII.GetByteCount(pdfObject);
        }

        var xrefOffset = runningLength;
        pdf.Append($"xref\n0 {objects.Count + 1}\n");
        pdf.Append("0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            pdf.Append(offset.ToString("D10")).Append(" 00000 n \n");
        }

        pdf.Append("trailer << /Size ").Append(objects.Count + 1).Append(" /Root 1 0 R >>\n");
        pdf.Append("startxref\n").Append(xrefOffset).Append("\n%%EOF");

        return Encoding.ASCII.GetBytes(pdf.ToString());
    }
}
