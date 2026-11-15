using System.Threading;
using System.Threading.Tasks;
using Palantyr.Contracts;

namespace Palantyr.Domain;

/// <summary>
/// Generator interface for session summaries adhering to strict ADR-006 pre-generation privacy filtering.
/// </summary>
public interface ISessionSummaryGenerator
{
    Task<SessionSummaryDto> GenerateSummaryAsync(
        string campaignId,
        string sessionId,
        GenerateSummaryRequest request,
        CancellationToken cancellationToken = default);

    Task<SessionSummaryDto> PropagateSummaryCorrectionAsync(
        string campaignId,
        string summaryId,
        UpdateSummaryRequest request,
        CancellationToken cancellationToken = default);
}
