using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using SelfCarePortal.Models;
using SelfCarePortal.Services;

namespace SelfCarePortal.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IPortalDataService _portalDataService;

    public HomeController(ILogger<HomeController> logger, IPortalDataService portalDataService)
    {
        _logger = logger;
        _portalDataService = portalDataService;
    }

    public IActionResult Index()
    {
        return View(_portalDataService.GetPortal());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ToggleFeature(FeatureUpdateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", _portalDataService.GetPortal("The feature update request is invalid.", "danger"));
        }

        var message = _portalDataService.ToggleFeature(request);
        return View("Index", _portalDataService.GetPortal(message, "success"));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult PurchaseAddOn(AddOnPurchaseRequest request)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", _portalDataService.GetPortal("The add-on purchase request is invalid.", "danger"));
        }

        var message = _portalDataService.PurchaseAddOn(request);
        return View("Index", _portalDataService.GetPortal(message, "success"));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult PayInvoice(PayInvoiceRequest request)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", _portalDataService.GetPortal("The payment request is invalid.", "danger"));
        }

        var message = _portalDataService.PayInvoice(request);
        return View("Index", _portalDataService.GetPortal(message, "success"));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AskAssistant(AssistantPromptRequest request)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", _portalDataService.GetPortal("Please enter a question for the assistant.", "danger"));
        }

        var response = _portalDataService.AskAssistant(request.Question);
        return View("Index", _portalDataService.GetPortal("Assistant response generated.", "info", request.Question, response));
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

        var offsets = new List<int> { 0 };
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
        for (var index = 1; index < offsets.Count; index++)
        {
            pdf.Append(offsets[index].ToString("D10")).Append(" 00000 n \n");
        }

        pdf.Append("trailer << /Size ").Append(objects.Count + 1).Append(" /Root 1 0 R >>\n");
        pdf.Append("startxref\n").Append(xrefOffset).Append("\n%%EOF");

        return Encoding.ASCII.GetBytes(pdf.ToString());
    }
}
