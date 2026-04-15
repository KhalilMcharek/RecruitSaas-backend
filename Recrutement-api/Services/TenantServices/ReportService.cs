using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Recrutement_api.Data;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

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

            // QuestPDF Community licence (free for open-source / academic)
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
                ws.Cell(r, 7).Value = c.AnalyseCV?.Score != null ? (object)Math.Round((double)c.AnalyseCV.Score) : "—";
                ws.Cell(r, 8).Value = c.AnalyseCV?.Classification ?? "—";

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

            var nom          = c.Candidat?.Utilisateur?.Nom ?? "Candidate";
            var email        = c.Candidat?.Utilisateur?.Email ?? "—";
            var offre        = c.Offre?.Titre ?? "—";
            var entreprise   = c.Offre?.Entreprise?.Nom ?? "—";
            var statut       = c.Statut;
            var date         = c.CreeLe.ToString("dd MMM yyyy");
            var score        = c.AnalyseCV?.Score != null ? $"{Math.Round((double)c.AnalyseCV.Score)}%" : "N/A";
            var classif      = c.AnalyseCV?.Classification ?? "—";
            var resume       = c.AnalyseCV?.Resume ?? "No AI summary available.";
            var competences  = c.AnalyseCV?.Competences ?? "";
            var experience   = c.AnalyseCV?.Experience ?? "";

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(10).FontColor("#334155"));

                    page.Content().Column(col =>
                    {
                        // ── Header banner ──
                        col.Item().Background("#454a83").Padding(20).Row(row =>
                        {
                            row.RelativeItem().Column(inner =>
                            {
                                inner.Item().Text(nom).FontSize(20).Bold().FontColor("#FFFFFF");
                                inner.Item().Text(email).FontSize(10).FontColor("#CBD5E1");
                            });

                            row.ConstantItem(120).AlignRight().Column(inner =>
                            {
                                inner.Item().Text("Candidate Report").FontSize(11).FontColor("#94A3B8");
                                inner.Item().Text(date).FontSize(10).FontColor("#94A3B8");
                            });
                        });

                        col.Item().Height(16);

                        // ── Info grid ──
                        col.Item().Background("#F5F7FA").Padding(14).Grid(grid =>
                        {
                            grid.Columns(2);
                            grid.Item().Column(inner =>
                            {
                                inner.Item().Text("Job Offer").FontSize(9).FontColor("#94A3B8").Bold();
                                inner.Item().Text(offre).FontSize(11).Bold();
                            });
                            grid.Item().Column(inner =>
                            {
                                inner.Item().Text("Company").FontSize(9).FontColor("#94A3B8").Bold();
                                inner.Item().Text(entreprise).FontSize(11).Bold();
                            });
                            grid.Item().PaddingTop(8).Column(inner =>
                            {
                                inner.Item().Text("Status").FontSize(9).FontColor("#94A3B8").Bold();
                                inner.Item().Text(statut).FontSize(11).Bold();
                            });
                            grid.Item().PaddingTop(8).Column(inner =>
                            {
                                inner.Item().Text("AI Score").FontSize(9).FontColor("#94A3B8").Bold();
                                inner.Item().Text($"{score}  ({classif})").FontSize(11).Bold();
                            });
                        });

                        col.Item().Height(16);

                        // ── AI Summary ──
                        col.Item().Text("AI Summary").FontSize(11).Bold().FontColor("#0F172A");
                        col.Item().Height(6);
                        col.Item().Background("#F8FAFC").BorderLeft(3).BorderColor("#1D9E75").Padding(12)
                            .Text(resume).FontSize(10).LineHeight(1.6f);

                        col.Item().Height(16);

                        // ── Skills ──
                        if (!string.IsNullOrWhiteSpace(competences))
                        {
                            col.Item().Text("Key Skills").FontSize(11).Bold().FontColor("#0F172A");
                            col.Item().Height(6);
                            col.Item().Text(competences).FontSize(10).LineHeight(1.5f);
                            col.Item().Height(14);
                        }

                        // ── Experience ──
                        if (!string.IsNullOrWhiteSpace(experience))
                        {
                            col.Item().Text("Experience").FontSize(11).Bold().FontColor("#0F172A");
                            col.Item().Height(6);
                            col.Item().Text(experience).FontSize(10).LineHeight(1.5f);
                        }
                    });

                    page.Footer().AlignCenter()
                        .Text(t =>
                        {
                            t.Span("Generated by RecruitSaaS  •  ").FontSize(8).FontColor("#94A3B8");
                            t.Span(DateTime.UtcNow.ToString("dd MMM yyyy HH:mm")).FontSize(8).FontColor("#94A3B8");
                        });
                });
            });

            return document.GeneratePdf();
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

            var nom        = c.Candidat?.Utilisateur?.Nom ?? "Candidate";
            var email      = c.Candidat?.Utilisateur?.Email ?? "—";
            var offre      = c.Offre?.Titre ?? "—";
            var entreprise = c.Offre?.Entreprise?.Nom ?? "—";
            var statut     = c.Statut;
            var date       = c.CreeLe.ToString("dd MMM yyyy");
            var score      = c.AnalyseCV?.Score != null ? $"{Math.Round((double)c.AnalyseCV.Score)}%" : "N/A";
            var classif    = c.AnalyseCV?.Classification ?? "—";
            var resume     = c.AnalyseCV?.Resume ?? "No AI summary available.";
            var competences = c.AnalyseCV?.Competences ?? "—";
            var experience  = c.AnalyseCV?.Experience ?? "—";

            using var ms = new MemoryStream();
            using (var wordDoc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
            {
                var mainPart = wordDoc.AddMainDocumentPart();
                mainPart.Document = new Document(new Body());
                var body = mainPart.Document.Body!;

                void AddHeading(string text, int level = 1)
                {
                    var para = new Paragraph();
                    var run  = new Run(new DocumentFormat.OpenXml.Wordprocessing.Text(text));
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
                acceptees         = candidatures.Count(x => x.Statut == "Acceptée"),
                refusees          = candidatures.Count(x => x.Statut == "Refusée"),
                enAttente         = candidatures.Count(x => x.Statut == "Nouvelle" || x.Statut == "En cours"),
                scoreMoyenIA      = scores.Any() ? (double?)Math.Round(scores.Average()) : null,
                totalOffres       = offresCount
            };
        }
    }
}
