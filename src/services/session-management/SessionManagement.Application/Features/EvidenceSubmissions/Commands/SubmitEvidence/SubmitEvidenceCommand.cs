using MediatR;
using SessionManagement.Application.Features.SessionSnapshots;

namespace SessionManagement.Application.Features.EvidenceSubmissions;

public sealed record SubmitEvidenceCommand(Guid SessionTeamId, string QrHash) : IRequest<SubmitEvidenceResponse>;

/// <summary>
/// A Session Team's attempt to solve its current Treasure Hunt Play by scanning a QR.
/// </summary>
/// <param name="QrHash" example="9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08">Hash of the scanned QR. The server compares it against the Play's expected hash; a mismatch is recorded as an Invalid Attempt rather than an error.</param>
public sealed record SubmitEvidenceRequest(string QrHash);

