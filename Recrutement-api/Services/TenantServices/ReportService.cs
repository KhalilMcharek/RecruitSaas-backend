using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text.Json;

using Recrutement_api.Data;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Recrutement_api.Services.Interfaces;
using QuestDocument = QuestPDF.Fluent.Document;
using WordDocument = DocumentFormat.OpenXml.Wordprocessing.Document;

namespace Recrutement_api.Services.TenantServices
{
    public class ReportService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUser;

        public ReportService(ApplicationDbContext context, ICurrentUserService currentUser)
        {
            _context = context;
            _currentUser = currentUser;

            QuestPDF.Settings.License = LicenseType.Community;
        }

        // ─────────────────────────────────────────────────────────────
        // EXCEL: all candidates for a specific offer
        // ─────────────────────────────────────────────────────────────
        public async Task<byte[]> GenerateOfferCandidatesExcelAsync(Guid offreId)
        {
            var tenantId = _currentUser.TenantId;

            var candidatures = await _context.Candidatures
                .Where(c => c.OffreId == offreId && c.Offre.Entreprise.TenantId == tenantId)
                .Include(c => c.Candidat).ThenInclude(ca => ca.Utilisateur)
                .Include(c => c.Offre).ThenInclude(o => o.Entreprise)
                .Include(c => c.AnalyseCV)
                .OrderByDescending(c => c.CreeLe)
                .ToListAsync();

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Candidates");

            // --- Header row ---
            var headers = new[]
            {
                "Candidate Name", "Email", "Job Offer", "Company",
                "Date Applied", "Status", "AI Score (%)", "Classification"
            };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#454a83");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            // --- Data rows ---
            for (int row = 0; row < candidatures.Count; row++)
            {
                var c = candidatures[row];
                int r = row + 2;

                ws.Cell(r, 1).Value = c.Candidat?.Utilisateur?.Nom ?? "—";
                ws.Cell(r, 2).Value = c.Candidat?.Utilisateur?.Email ?? "—";
                ws.Cell(r, 3).Value = c.Offre?.Titre ?? "—";
                ws.Cell(r, 4).Value = c.Offre?.Entreprise?.Nom ?? "—";
                ws.Cell(r, 5).Value = c.CreeLe.ToString("dd MMM yyyy");
                ws.Cell(r, 6).Value = c.Statut;
                ws.Cell(r, 7).Value = c.AnalyseCV?.Score != null
                    ? Math.Round((double)c.AnalyseCV.Score)
                    : "—"; ws.Cell(r, 8).Value = c.AnalyseCV?.Classification ?? "—";

                // Color the score cell
                if (c.AnalyseCV?.Score != null)
                {
                    var score = c.AnalyseCV.Score;
                    ws.Cell(r, 7).Style.Font.FontColor = score >= 80
                        ? XLColor.FromHtml("#16a34a")
                        : score >= 50 ? XLColor.FromHtml("#d97706")
                        : XLColor.FromHtml("#dc2626");
                    ws.Cell(r, 7).Style.Font.Bold = true;
                }

                // Zebra striping
                if (row % 2 == 1)
                {
                    for (int col = 1; col <= 8; col++)
                        ws.Cell(r, col).Style.Fill.BackgroundColor = XLColor.FromHtml("#F5F7FA");
                }
            }

            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return ms.ToArray();
        }

        // ─────────────────────────────────────────────────────────────
        // PDF: full candidate report
        // ─────────────────────────────────────────────────────────────
        public async Task<byte[]> GenerateCandidatePdfAsync(Guid candidatureId)
        {
            var tenantId = _currentUser.TenantId;

            var c = await _context.Candidatures
                .Where(x => x.Id == candidatureId && x.Offre.Entreprise.TenantId == tenantId)
                .Include(x => x.Candidat).ThenInclude(ca => ca.Utilisateur)
                .Include(x => x.Offre).ThenInclude(o => o.Entreprise)
                .Include(x => x.AnalyseCV)
                .FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("Candidature not found or access denied.");

            // --- JSON Deserialization Logic ---
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            // 1. Parse Summary (Object)
            string finalResume = "No AI summary available.";
            try
            {
                var summaryObj = JsonSerializer.Deserialize<SummaryData>(c.AnalyseCV?.Resume ?? "{}", options);
                finalResume = summaryObj?.Summary ?? "No AI summary available.";
            }
            catch { finalResume = c.AnalyseCV?.Resume; }

            // 2. Parse Skills (List of strings)
            var skillsList = new List<string>();
            try
            {
                skillsList = JsonSerializer.Deserialize<List<string>>(c.AnalyseCV?.Competences ?? "[]", options) ?? new List<string>();
            }
            catch { /* Fallback or handle error */ }

            // 3. Parse Experience (List of objects)
            var experienceList = new List<ExperienceData>();
            try
            {
                experienceList = JsonSerializer.Deserialize<List<ExperienceData>>(c.AnalyseCV?.Experience ?? "[]", options) ?? new List<ExperienceData>();
            }
            catch { /* Fallback */ }

            // --- PDF Generation ---
            var nom = c.Candidat?.Utilisateur?.Nom ?? "Candidate";
            var email = c.Candidat?.Utilisateur?.Email ?? "—";
            var offre = c.Offre?.Titre ?? "—";
            var entreprise = c.Offre?.Entreprise?.Nom ?? "—";
            var statut = c.Statut;
            var date = c.CreeLe.ToString("dd MMM yyyy");
            var score = c.AnalyseCV?.Score != null ? $"{Math.Round((double)c.AnalyseCV.Score)}%" : "N/A";
            var classif = c.AnalyseCV?.Classification ?? "—";

            var document = QuestDocument.Create(container => {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(10).FontColor("#334155"));

                    page.Content().Column(col =>
                    {
                        // Header (Same as yours)
                        col.Item().Background("#454a83").Padding(20).Row(row => {
                            row.RelativeItem().Column(inner => {
                                inner.Item().Text(nom).FontSize(20).Bold().FontColor("#FFFFFF");
                                inner.Item().Text(email).FontSize(10).FontColor("#CBD5E1");
                            });
                            row.ConstantItem(120).AlignRight().Text(date).FontColor("#94A3B8");
                        });

                        col.Item().Height(16);

                        // Info Grid (Same as yours)
                        col.Item().Background("#F5F7FA").Padding(14).Grid(grid => {
                            grid.Columns(2);
                            grid.Item().Text($"Job: {offre}").Bold();
                            grid.Item().Text($"Score: {score} ({classif})").Bold();
                        });

                        col.Item().Height(16);

                        // ── AI Summary ──
                        col.Item().Text("AI Summary").FontSize(11).Bold().FontColor("#0F172A");
                        col.Item().Height(6);
                        col.Item().Background("#F8FAFC").BorderLeft(3).BorderColor("#1D9E75").Padding(12)
                            .Text(finalResume).FontSize(10).LineHeight(1.6f);

                        col.Item().Height(16);

                        // ── Key Skills (Rendered as Comma Separated or Tags) ──
                        if (skillsList.Any())
                        {
                            col.Item().Text("Key Skills").FontSize(11).Bold().FontColor("#0F172A");
                            col.Item().Height(6);
                            col.Item().Text(string.Join(" • ", skillsList)).FontSize(10).Italic();
                            col.Item().Height(14);
                        }

                        // ── Experience (Rendered as a List) ──
                        if (experienceList.Any())
                        {
                            col.Item().Text("Professional Experience").FontSize(11).Bold().FontColor("#0F172A");
                            col.Item().Height(8);

                            foreach (var exp in experienceList)
                            {
                                col.Item().PaddingBottom(10).Column(expCol =>
                                {
                                    expCol.Item().Row(r => {
                                        r.RelativeItem().Text(exp.Role).Bold();
                                        r.ConstantItem(100).AlignRight().Text(exp.Years).FontSize(9).Italic();
                                    });
                                    expCol.Item().PaddingLeft(10).Text(exp.Summary).FontSize(9).FontColor("#475569");
                                });
                            }
                        }
                    });

                    page.Footer().AlignCenter().Text(t => {
                        t.Span("Generated by RecruitSaaS • ").FontSize(8);
                        t.Span(DateTime.UtcNow.ToString("g")).FontSize(8);
                    });
                });
            });

            return document.GeneratePdf();
        }

        // Helper classes for Deserialization
        public class SummaryData { public string Summary { get; set; } }
        public class ExperienceData
        {
            public string Role { get; set; }
            public string Years { get; set; }
            public string Summary { get; set; }
        }

        // ─────────────────────────────────────────────────────────────
        // WORD (.docx): full candidate report via OpenXml
        // ─────────────────────────────────────────────────────────────
        public async Task<byte[]> GenerateCandidateWordAsync(Guid candidatureId)
        {
            var tenantId = _currentUser.TenantId;

            var c = await _context.Candidatures
                .Where(x => x.Id == candidatureId && x.Offre.Entreprise.TenantId == tenantId)
                .Include(x => x.Candidat).ThenInclude(ca => ca.Utilisateur)
                .Include(x => x.Offre).ThenInclude(o => o.Entreprise)
                .Include(x => x.AnalyseCV)
                .FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("Candidature not found or access denied.");

            var nom = c.Candidat?.Utilisateur?.Nom ?? "Candidate";
            var email = c.Candidat?.Utilisateur?.Email ?? "—";
            var offre = c.Offre?.Titre ?? "—";
            var entreprise = c.Offre?.Entreprise?.Nom ?? "—";
            var statut = c.Statut;
            var date = c.CreeLe.ToString("dd MMM yyyy");
            var score = c.AnalyseCV?.Score != null ? $"{Math.Round((double)c.AnalyseCV.Score)}%" : "N/A";
            var classif = c.AnalyseCV?.Classification ?? "—";
            var resume = c.AnalyseCV?.Resume ?? "No AI summary available.";
            var competences = c.AnalyseCV?.Competences ?? "—";
            var experience = c.AnalyseCV?.Experience ?? "—";

            using var ms = new MemoryStream();
            using (var wordDoc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
            {
                var mainPart = wordDoc.AddMainDocumentPart();
                mainPart.Document = new WordDocument(new Body());
                var body = mainPart.Document.Body!;

                void AddHeading(string text, int level = 1)
                {
                    var para = new Paragraph();
                    var run = new Run(new DocumentFormat.OpenXml.Wordprocessing.Text(text));
                    run.PrependChild(new RunProperties(new Bold(), new FontSize { Val = level == 1 ? "28" : "22" }));
                    para.Append(run);
                    body.Append(para);
                }

                void AddParagraph(string label, string value)
                {
                    var para = new Paragraph();
                    var labelRun = new Run(new DocumentFormat.OpenXml.Wordprocessing.Text($"{label}: "));
                    labelRun.PrependChild(new RunProperties(new Bold()));
                    var valueRun = new Run(new DocumentFormat.OpenXml.Wordprocessing.Text(value));
                    para.Append(labelRun, valueRun);
                    body.Append(para);
                }

                void AddText(string text)
                {
                    body.Append(new Paragraph(new Run(new DocumentFormat.OpenXml.Wordprocessing.Text(text))));
                }

                void AddBlank() => body.Append(new Paragraph());

                // Document content
                AddHeading($"Candidate Report — {nom}");
                AddBlank();
                AddParagraph("Email", email);
                AddParagraph("Job Offer", offre);
                AddParagraph("Company", entreprise);
                AddParagraph("Date Applied", date);
                AddParagraph("Status", statut);
                AddParagraph("AI Score", $"{score} ({classif})");
                AddBlank();
                AddHeading("AI Summary", 2);
                AddText(resume);
                AddBlank();
                AddHeading("Key Skills", 2);
                AddText(competences);
                AddBlank();
                AddHeading("Experience", 2);
                AddText(experience);
                AddBlank();
                AddText($"Generated by RecruitSaaS on {DateTime.UtcNow:dd MMM yyyy HH:mm} UTC");

                mainPart.Document.Save();
            }

            return ms.ToArray();
        }

        // ─────────────────────────────────────────────────────────────
        // KPI: global stats (for Reports dashboard)
        // ─────────────────────────────────────────────────────────────
        public async Task<object> GetGlobalKpisAsync()
        {
            var tenantId = _currentUser.TenantId;

            var candidatures = await _context.Candidatures
                .Where(c => c.Offre.Entreprise.TenantId == tenantId)
                .Select(c => new { c.Statut, Score = c.AnalyseCV != null ? c.AnalyseCV.Score : (float?)null })
                .ToListAsync();

            var offresCount = await _context.OffresEmploi
                .Where(o => o.Entreprise.TenantId == tenantId && o.EstPublie)
                .CountAsync();

            var scores = candidatures.Where(x => x.Score.HasValue).Select(x => (double)x.Score!.Value).ToList();

            return new
            {
                totalCandidatures = candidatures.Count,
                acceptees = candidatures.Count(x => x.Statut == "Acceptée"),
                refusees = candidatures.Count(x => x.Statut == "Refusée"),
                enAttente = candidatures.Count(x => x.Statut == "Nouvelle" || x.Statut == "En cours"),
                scoreMoyenIA = scores.Any() ? (double?)Math.Round(scores.Average()) : null,
                totalOffres = offresCount
            };
        }
    }
}
