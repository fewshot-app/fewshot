using Apex.Core.Enums;

namespace Apex.Core.Models;

public class Outcome
{
    public int OutcomeId { get; set; }
    public int SuggestionId { get; set; }
    public OutcomeStatus Status { get; set; }
    public string? Notes { get; set; }
    public string? ErrorCode { get; set; }
    public int? EffortSavedMinutes { get; set; }
    public bool ConfirmedByGit { get; set; }
    public bool IsExplicit { get; set; }
    public DateTime FeedbackAt { get; set; }
}
