using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Recrutement_api.Data;
using System.Text.Json;

namespace Recrutement_api.Services.StatusPromotion
{
    /// <summary>
    /// Result returned after applying score-based promotion rules to a candidature.
    /// </summary>
    public sealed record ScorePromotionResult(
        string PreviousStatus,
        string NewStatus,
        bool   StatusChanged,
        bool   EnableQuiz,
        string Priority,
        string[] Flags
    );

   
    public class CandidateStatusService
    {
        // ── Score thresholds ──────────────────────────────────────────────────
        private const float HighScoreThreshold   = 80f;
        private const float MediumScoreThreshold = 50f;

        // ── Status constants (match DB / frontend values) ─────────────────────
        private const string StatusNew          = "Nouvelle";
        private const string StatusUnderReview  = "En cours";
        private const string StatusShortlisted  = "Présélectionné";

        // ── Flag constants ────────────────────────────────────────────────────
        private const string FlagLowScore       = "LOW_SCORE";
        private const string PriorityLow        = "LOW";

        private readonly ApplicationDbContext _db;
        private readonly ILogger<CandidateStatusService> _logger;

        public CandidateStatusService(
            ApplicationDbContext db,
            ILogger<CandidateStatusService> logger)
        {
            _db     = db;
            _logger = logger;
        }

        /// <summary>
        /// Applies promotion rules based on the AI score.
        /// Only transitions candidatures that are currently in "Nouvelle" status.
        /// </summary>
        /// <param name="candidatureId">Target candidature.</param>
        /// <param name="score">AI score in the range [0, 100].</param>
        /// <returns>A <see cref="ScorePromotionResult"/> describing what changed.</returns>
        public async Task<ScorePromotionResult> ApplyScoreRulesAsync(Guid candidatureId, float score)
        {
            var candidature = await _db.Candidatures
                .FirstOrDefaultAsync(c => c.Id == candidatureId);

            if (candidature == null)
            {
                _logger.LogWarning("[StatusPromotion] Candidature {Id} not found.", candidatureId);
                return new ScorePromotionResult(
                    PreviousStatus: "Unknown",
                    NewStatus:      "Unknown",
                    StatusChanged:  false,
                    EnableQuiz:     false,
                    Priority:       null!,
                    Flags:          Array.Empty<string>()
                );
            }

            var previousStatus = candidature.Statut;

            // Guard: only auto-promote from "Nouvelle".
            // Any other status has already been set by a human or a previous automation run.
            if (previousStatus != StatusNew)
            {
                _logger.LogInformation(
                    "[StatusPromotion] Candidature {Id} is already in status '{Status}'. Skipping promotion.",
                    candidatureId, previousStatus);

                return new ScorePromotionResult(
                    PreviousStatus: previousStatus,
                    NewStatus:      previousStatus,
                    StatusChanged:  false,
                    EnableQuiz:     candidature.EnableQuiz ?? false,
                    Priority:       candidature.Priority!,
                    Flags:          DeserializeFlags(candidature.Flags)
                );
            }

            // ── Apply business rules ──────────────────────────────────────────
            string   newStatus   = StatusUnderReview;
            bool     enableQuiz  = false;
            string?  priority    = null;
            string[] flags       = Array.Empty<string>();

            if (score >= HighScoreThreshold)
            {
                // score >= 80 → SHORTLISTED + enable quiz prompt
                newStatus  = StatusShortlisted;
                enableQuiz = true;

                _logger.LogInformation(
                    "[StatusPromotion] Candidature {Id} | score={Score} → {Status} (EnableQuiz=true)",
                    candidatureId, score, newStatus);
            }
            else if (score >= MediumScoreThreshold)
            {
                // 50 <= score < 80 → UNDER REVIEW, no special flags
                newStatus = StatusUnderReview;

                _logger.LogInformation(
                    "[StatusPromotion] Candidature {Id} | score={Score} → {Status}",
                    candidatureId, score, newStatus);
            }
            else
            {
                // score < 50 → UNDER REVIEW + LOW priority + LOW_SCORE flag
                newStatus = StatusUnderReview;
                priority  = PriorityLow;
                flags     = new[] { FlagLowScore };

                _logger.LogInformation(
                    "[StatusPromotion] Candidature {Id} | score={Score} → {Status} (Priority=LOW, Flags=[LOW_SCORE])",
                    candidatureId, score, newStatus);
            }

            // ── Persist ───────────────────────────────────────────────────────
            candidature.Statut     = newStatus;
            candidature.EnableQuiz = enableQuiz;
            candidature.Priority   = priority;
            candidature.Flags      = flags.Length > 0
                ? JsonSerializer.Serialize(flags)
                : null;

            _db.Candidatures.Update(candidature);
            await _db.SaveChangesAsync();

            return new ScorePromotionResult(
                PreviousStatus: previousStatus,
                NewStatus:      newStatus,
                StatusChanged:  true,
                EnableQuiz:     enableQuiz,
                Priority:       priority!,
                Flags:          flags
            );
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string[] DeserializeFlags(string? flagsJson)
        {
            if (string.IsNullOrWhiteSpace(flagsJson)) return Array.Empty<string>();
            try { return JsonSerializer.Deserialize<string[]>(flagsJson) ?? Array.Empty<string>(); }
            catch { return Array.Empty<string>(); }
        }
    }
}
