using System.Threading;
using System.Threading.Tasks;
using Palantyr.Contracts;

namespace Palantyr.Domain;

/// <summary>
/// Eonix Tabletop Memory Keeper interface (ADR-007, EPIC-10).
/// </summary>
public interface IEonixAssistantService
{
    Task<EonixChatQueryResponse> AskAsync(
        string campaignId,
        EonixChatQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<EonixTeachResponse> TeachAsync(
        string campaignId,
        EonixTeachRequest request,
        CancellationToken cancellationToken = default);
}
