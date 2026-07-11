using System.ComponentModel.DataAnnotations.Schema;
using SessionManagement.Domain.Abstractions;
using Umbral.ServiceDefaults;

namespace SessionManagement.Domain.LiveSessions;

public sealed class LiveSession : AggregateRoot
{
    private LiveSession()
    {
    }

    private LiveSession(
        Guid id,
        Guid missionId,
        string missionName,
        string name,
        LiveSessionState state,
        DateTimeOffset? scheduledStartAtUtc,
        DateTimeOffset createdAtUtc,
        IReadOnlyList<LiveSessionStage> sessionStageFlow)
    {
        Id = id;
        MissionId = missionId;
        MissionName = missionName;
        Name = name;
        State = state;
        ScheduledStartAtUtc = scheduledStartAtUtc;
        CreatedAtUtc = createdAtUtc;
        SessionStageFlow = sessionStageFlow;
    }

    public Guid Id { get; private set; }

    public Guid MissionId { get; private set; }

    public string MissionName { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public LiveSessionState State { get; private set; }

    public DateTimeOffset? ScheduledStartAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public long SequenceNumber { get; private set; }

    public string? JoinCodeValue { get; private set; }

    public DateTimeOffset? EnrollmentWindowOpenedAtUtc { get; private set; }

    public DateTimeOffset? EnrollmentWindowClosedAtUtc { get; private set; }

    public ICollection<SessionTeam> SessionTeams { get; private set; } = new List<SessionTeam>();

    public ICollection<TeamParticipation> TeamParticipations { get; private set; } = new List<TeamParticipation>();

    public ICollection<SessionTeamProgress> TeamProgressions { get; private set; } = new List<SessionTeamProgress>();

    public ICollection<EvidenceSubmission> EvidenceSubmissions { get; private set; } = new List<EvidenceSubmission>();

    public ICollection<ValidationOverrideLog> ValidationOverrideLogs { get; private set; } = new List<ValidationOverrideLog>();

    public ICollection<ReleasedHint> ReleasedHints { get; private set; } = new List<ReleasedHint>();

    public IReadOnlyList<LiveSessionStage> SessionStageFlow { get; private set; } = Array.Empty<LiveSessionStage>();

    [NotMapped]
    public EnrollmentWindow EnrollmentWindow => new(EnrollmentWindowOpenedAtUtc, EnrollmentWindowClosedAtUtc);

    public static LiveSession Create(
        Guid id,
        Guid missionId,
        string missionName,
        string name,
        DateTimeOffset? scheduledStartAtUtc,
        DateTimeOffset createdAtUtc,
        IReadOnlyList<LiveSessionStage> sessionStageFlow)
    {
        var liveSession = new LiveSession(
            id == Guid.Empty ? Guid.NewGuid() : id,
            NormalizeGuid(missionId, "live_session_mission_required", "LiveSession must reference a Mission."),
            NormalizeRequiredText(missionName, "live_session_mission_name_required", "Mission name is required.", 120),
            NormalizeRequiredText(name, "live_session_name_required", "LiveSession name is required.", 120),
            LiveSessionState.Scheduled,
            scheduledStartAtUtc,
            createdAtUtc,
            Array.Empty<LiveSessionStage>());

        liveSession.ReplaceSessionStageFlow(sessionStageFlow);

        return liveSession;
    }

    public void ReplaceSessionStageFlow(IReadOnlyList<LiveSessionStage> sessionStageFlow)
    {
        ArgumentNullException.ThrowIfNull(sessionStageFlow);

        SessionStageFlow = NormalizeSessionStageFlow(sessionStageFlow);
    }

    public void Start(DateTimeOffset startedAtUtc)
    {
        EnsureState(
            LiveSessionState.Scheduled,
            "live_session_cannot_start",
            "Only a Scheduled LiveSession can start.");

        if (SessionTeams.Count == 0)
        {
            throw new UmbralDomainException(
                "live_session_requires_session_teams",
                "LiveSession cannot start without at least one Session Team registered.",
                UmbralFailureCategory.Conflict);
        }

        if (EnrollmentWindowOpenedAtUtc is not null && EnrollmentWindowClosedAtUtc is null)
        {
            CloseEnrollmentWindow(startedAtUtc);
        }

        State = LiveSessionState.Active;
    }

    public void Pause()
    {
        EnsureState(
            LiveSessionState.Active,
            "live_session_cannot_pause",
            "Only an Active LiveSession can pause.");

        State = LiveSessionState.Paused;
    }

    public void Resume()
    {
        EnsureState(
            LiveSessionState.Paused,
            "live_session_cannot_resume",
            "Only a Paused LiveSession can resume.");

        State = LiveSessionState.Active;
    }

    public void FinalizeSession()
    {
        if (State == LiveSessionState.Finalized)
        {
            return;
        }

        EnsureState(
            LiveSessionState.Active,
            LiveSessionState.Paused,
            "live_session_cannot_finalize",
            "Only an Active or Paused LiveSession can finalize.");

        State = LiveSessionState.Finalized;
    }

    public IReadOnlyList<ReleasedHint> FinalizeAndRevealAllHints(DateTimeOffset releasedAtUtc)
    {
        FinalizeSession();

        var releasedHints = new List<ReleasedHint>();
        var orderedStages = SessionStageFlow.OrderBy(stage => stage.SessionStageOrder).ToArray();

        foreach (var sessionTeam in SessionTeams)
        {
            foreach (var sessionStage in orderedStages)
            {
                foreach (var sessionStageHint in sessionStage.Hints)
                {
                    if (ReleasedHints.Any(releasedHint =>
                        releasedHint.SessionTeamId == sessionTeam.Id
                        && releasedHint.MissionStageId == sessionStage.MissionStageId
                        && releasedHint.HintId == sessionStageHint.Id))
                    {
                        continue;
                    }

                    var releasedHint = ReleasedHint.Create(
                        Id,
                        sessionTeam.Id,
                        sessionStage.MissionStageId,
                        sessionStageHint.Id,
                        releasedAtUtc,
                        "Rule");
                    ReleasedHints.Add(releasedHint);
                    releasedHints.Add(releasedHint);
                }
            }
        }

        if (releasedHints.Count > 0)
        {
            SequenceNumber++;
        }

        return releasedHints;
    }

    public void Cancel()
    {
        EnsureState(
            LiveSessionState.Scheduled,
            LiveSessionState.Active,
            LiveSessionState.Paused,
            "live_session_cannot_cancel",
            "Only a Scheduled, Active or Paused LiveSession can cancel.");

        State = LiveSessionState.Canceled;
    }

    public void AssignJoinCode(JoinCode joinCode)
    {
        ArgumentNullException.ThrowIfNull(joinCode);

        if (JoinCodeValue is null)
        {
            JoinCodeValue = joinCode.Value;
            return;
        }

        if (string.Equals(JoinCodeValue, joinCode.Value, StringComparison.Ordinal))
        {
            return;
        }

        throw new UmbralDomainException(
            "live_session_join_code_already_assigned",
            "LiveSession already has a Join Code assigned.",
            UmbralFailureCategory.Conflict);
    }

    public void OpenEnrollmentWindow(DateTimeOffset openedAtUtc)
    {
        EnsureScheduled();

        if (JoinCodeValue is null)
        {
            throw new UmbralDomainException(
                "live_session_join_code_required_before_enrollment",
                "A Join Code must be generated before opening enrollment.",
                UmbralFailureCategory.Validation);
        }

        if (EnrollmentWindowClosedAtUtc is not null)
        {
            throw new UmbralDomainException(
                "live_session_enrollment_window_closed",
                "Enrollment window is already closed.",
                UmbralFailureCategory.Conflict);
        }

        if (EnrollmentWindowOpenedAtUtc is not null)
        {
            return;
        }

        EnrollmentWindowOpenedAtUtc = openedAtUtc;
    }

    public void CloseEnrollmentWindow(DateTimeOffset closedAtUtc)
    {
        if (EnrollmentWindowOpenedAtUtc is null)
        {
            throw new UmbralDomainException(
                "live_session_enrollment_window_not_opened",
                "Enrollment window has not been opened.",
                UmbralFailureCategory.Validation);
        }

        if (EnrollmentWindowClosedAtUtc is not null)
        {
            return;
        }

        if (closedAtUtc < EnrollmentWindowOpenedAtUtc)
        {
            throw new UmbralDomainException(
                "live_session_enrollment_window_close_time_invalid",
                "Enrollment window cannot close before it opens.",
                UmbralFailureCategory.Validation);
        }

        EnrollmentWindowClosedAtUtc = closedAtUtc;
    }

    /// <summary>
    /// Creates a Session Team on behalf of a participant, validating the presented join code and the
    /// open enrollment window. This does not enrol the participant — the caller (Application layer)
    /// orchestrates registration and enrollment by following this with <see cref="EnrollParticipantInTeam"/>.
    /// </summary>
    public SessionTeam RegisterTeam(
        Guid sessionTeamId,
        string teamName,
        JoinCode presentedJoinCode,
        DateTimeOffset registeredAtUtc)
    {
        EnsureEnrollmentAllowed(presentedJoinCode, registeredAtUtc);

        var normalizedTeamName = SessionTeam.NormalizeTeamName(teamName);
        if (SessionTeams.Any(team => string.Equals(team.NormalizedName, normalizedTeamName, StringComparison.Ordinal)))
        {
            throw new UmbralDomainException(
                "session_team_name_duplicate",
                "Session Team name already exists in this LiveSession.",
                UmbralFailureCategory.Conflict);
        }

        var sessionTeam = SessionTeam.Create(Id, sessionTeamId, teamName, registeredAtUtc);
        SessionTeams.Add(sessionTeam);

        return sessionTeam;
    }

    /// <summary>
    /// Creates an empty Session Team on behalf of the operator so players can join it later. Unlike
    /// <see cref="RegisterTeam"/>, this does not enrol a participant and does not require a join code
    /// or an open enrollment window — the operator is trusted and may prepare teams before opening
    /// enrollment. Only allowed while the LiveSession is still Scheduled (i.e. not started).
    /// </summary>
    public SessionTeam RegisterTeamByOperator(
        Guid sessionTeamId,
        string teamName,
        DateTimeOffset createdAtUtc)
    {
        EnsureScheduled();

        var normalizedTeamName = SessionTeam.NormalizeTeamName(teamName);
        if (SessionTeams.Any(team => string.Equals(team.NormalizedName, normalizedTeamName, StringComparison.Ordinal)))
        {
            throw new UmbralDomainException(
                "session_team_name_duplicate",
                "Session Team name already exists in this LiveSession.",
                UmbralFailureCategory.Conflict);
        }

        var sessionTeam = SessionTeam.Create(Id, sessionTeamId, teamName, createdAtUtc);
        SessionTeams.Add(sessionTeam);

        return sessionTeam;
    }

    public TeamParticipation EnrollParticipantInTeam(
        Guid sessionTeamId,
        string participantUserId,
        JoinCode presentedJoinCode,
        DateTimeOffset enrolledAtUtc)
    {
        EnsureEnrollmentAllowed(presentedJoinCode, enrolledAtUtc);

        if (SessionTeams.All(team => team.Id != sessionTeamId))
        {
            throw new UmbralDomainException(
                "session_team_not_found",
                "Session Team does not belong to this LiveSession.",
                UmbralFailureCategory.NotFound);
        }

        var normalizedParticipantUserId = ParticipantUserId.Parse(participantUserId);
        var existingParticipation = TeamParticipations.FirstOrDefault(participation =>
            string.Equals(participation.ParticipantUserId, normalizedParticipantUserId.Value, StringComparison.Ordinal));

        if (existingParticipation is null)
        {
            var participation = TeamParticipation.Create(Id, sessionTeamId, normalizedParticipantUserId, enrolledAtUtc);
            TeamParticipations.Add(participation);
            return participation;
        }

        if (existingParticipation.SessionTeamId == sessionTeamId)
        {
            return existingParticipation;
        }

        existingParticipation.MoveToTeam(sessionTeamId, enrolledAtUtc);
        return existingParticipation;
    }

    public bool IsEnrollmentOpenAt(DateTimeOffset nowUtc) => EnrollmentWindow.IsOpenAt(nowUtc);

    public EvidenceSubmission SubmitEvidence(Guid sessionTeamId, string qrHash, DateTimeOffset submittedAtUtc)
    {
        EnsureEvidenceSubmissionAllowed();
        EnsureSessionTeamBelongsToLiveSession(sessionTeamId);

        var orderedStages = GetOrderedStages();
        var progress = GetOrCreateProgress(sessionTeamId, submittedAtUtc);
        EnsureTeamProgressAcceptsSubmission(progress);
        var currentStage = SelectCurrentStage(progress, orderedStages);
        EnsureTreasureHuntStage(currentStage);

        var normalizedQrHash = NormalizeRequiredText(
            qrHash,
            "evidence_submission_hash_required",
            "Evidence Submission QR hash is required.",
            EvidenceSubmission.SubmittedHashMaximumLength);

        EnsureStageNotAcceptedByTeam(sessionTeamId, currentStage.MissionStageId);

        var accepted = string.Equals(
            currentStage.ExpectedQrHash?.Trim(),
            normalizedQrHash,
            StringComparison.OrdinalIgnoreCase);
        var submissionOutcome = accepted ? ValidationOutcome.Accepted : ValidationOutcome.Rejected;
        var evidenceSubmission = EvidenceSubmission.CreateTreasureHunt(
            Id,
            sessionTeamId,
            currentStage,
            normalizedQrHash,
            submissionOutcome,
            accepted ? null : "qr_hash_mismatch",
            submittedAtUtc);

        EvidenceSubmissions.Add(evidenceSubmission);
        SequenceNumber++;
        RaiseEvidenceSubmittedEvents(evidenceSubmission, "AutomaticTreasureHunt", submittedAtUtc);

        if (!accepted)
        {
            return evidenceSubmission;
        }

        AcceptCurrentStage(progress, orderedStages, submittedAtUtc);
        return evidenceSubmission;
    }

    public EvidenceSubmission SubmitTriviaAnswer(Guid sessionTeamId, Guid selectedChoiceId, DateTimeOffset submittedAtUtc)
    {
        EnsureEvidenceSubmissionAllowed();
        EnsureSessionTeamBelongsToLiveSession(sessionTeamId);

        var orderedStages = GetOrderedStages();
        var progress = GetOrCreateProgress(sessionTeamId, submittedAtUtc);
        EnsureTeamProgressAcceptsSubmission(progress);
        var currentStage = SelectCurrentStage(progress, orderedStages);
        EnsureTriviaStage(currentStage, selectedChoiceId);

        EnsureStageNotAcceptedByTeam(sessionTeamId, currentStage.MissionStageId);

        var accepted = currentStage.CorrectChoiceId == selectedChoiceId;
        var submissionOutcome = accepted ? ValidationOutcome.Accepted : ValidationOutcome.Rejected;
        var evidenceSubmission = EvidenceSubmission.CreateTrivia(
            Id,
            sessionTeamId,
            currentStage,
            selectedChoiceId,
            submissionOutcome,
            accepted ? null : "trivia_answer_mismatch",
            submittedAtUtc);

        EvidenceSubmissions.Add(evidenceSubmission);
        SequenceNumber++;
        RaiseEvidenceSubmittedEvents(evidenceSubmission, "AutomaticTrivia", submittedAtUtc);

        if (accepted)
        {
            AcceptCurrentStage(progress, orderedStages, submittedAtUtc);
        }

        return evidenceSubmission;
    }

    public ValidationOverrideLog OverrideValidationOutcome(
        Guid evidenceSubmissionId,
        string operatorUserId,
        bool isAccepted,
        string reason,
        DateTimeOffset overriddenAtUtc)
    {
        var evidenceSubmission = EvidenceSubmissions.FirstOrDefault(submission => submission.Id == evidenceSubmissionId);
        if (evidenceSubmission is null)
        {
            throw new UmbralDomainException(
                "evidence_submission_not_found",
                "Evidence Submission does not belong to this LiveSession.",
                UmbralFailureCategory.NotFound);
        }

        var previousOutcome = evidenceSubmission.Outcome;
        var newOutcome = isAccepted ? ValidationOutcome.Accepted : ValidationOutcome.Rejected;
        var validationOverrideLog = ValidationOverrideLog.Create(
            Id,
            evidenceSubmission,
            operatorUserId,
            reason,
            previousOutcome,
            newOutcome,
            overriddenAtUtc);

        evidenceSubmission.ApplyOverride(newOutcome, isAccepted ? null : "operator_override_rejected");
        ValidationOverrideLogs.Add(validationOverrideLog);
        SequenceNumber++;
        RaiseDomainEvent(new ValidationOutcomeOverriddenDomainEvent(
            Id,
            evidenceSubmission.Id,
            evidenceSubmission.SessionTeamId,
            evidenceSubmission.MissionStageId,
            evidenceSubmission.Outcome.ToString(),
            reason,
            overriddenAtUtc));

        if (previousOutcome != ValidationOutcome.Accepted && newOutcome == ValidationOutcome.Accepted)
        {
            TryAcceptOverriddenStage(evidenceSubmission, overriddenAtUtc);
        }

        return validationOverrideLog;
    }

    public ReleasedHint ReleaseHint(
        Guid sessionTeamId,
        Guid hintId,
        DateTimeOffset releasedAtUtc,
        string unlockReason = "Manual")
    {
        EnsureHintReleaseAllowed();
        EnsureSessionTeamBelongsToLiveSession(sessionTeamId);

        var currentStage = GetCurrentStageForTeam(sessionTeamId);
        if (currentStage is null)
        {
            throw new UmbralDomainException(
                "released_hint_current_stage_required",
                "Session Team does not have a current Session Stage for Hint Release.",
                UmbralFailureCategory.Conflict);
        }

        var currentStageHint = currentStage.Hints.FirstOrDefault(hint => hint.Id == hintId);
        if (currentStageHint is null)
        {
            throw new UmbralDomainException(
                "released_hint_not_in_current_stage",
                "Hint must belong to the Session Team current Session Stage.",
                UmbralFailureCategory.Conflict);
        }

        if (ReleasedHints.Any(releasedHint =>
            releasedHint.SessionTeamId == sessionTeamId
            && releasedHint.MissionStageId == currentStage.MissionStageId
            && releasedHint.HintId == hintId))
        {
            throw new UmbralDomainException(
                "released_hint_duplicate",
                "Hint has already been released to this Session Team for this Session Stage.",
                UmbralFailureCategory.Conflict);
        }

        var releasedHint = ReleasedHint.Create(
            Id,
            sessionTeamId,
            currentStage.MissionStageId,
            currentStageHint.Id,
            releasedAtUtc,
            unlockReason);
        ReleasedHints.Add(releasedHint);
        SequenceNumber++;
        RaiseDomainEvent(new HintReleasedDomainEvent(
            Id,
            releasedHint.SessionTeamId,
            releasedHint.MissionStageId,
            releasedHint.HintId,
            releasedHint.UnlockReason,
            releasedAtUtc));

        return releasedHint;
    }

    public void EnsurePenaltyAllowed()
    {
        EnsureState(
            LiveSessionState.Active,
            LiveSessionState.Paused,
            "live_session_cannot_penalize",
            "LiveSession must be Active or Paused to apply a Penalty.");
    }

    public LiveSessionStageHint AddOperationalHint(
        Guid missionStageId,
        string content,
        double? latitude,
        double? longitude,
        DateTimeOffset createdAtUtc)
    {
        EnsureHintReleaseAllowed();

        var orderedStages = GetOrderedStages();
        var stageIndex = Array.FindIndex(orderedStages, stage => stage.MissionStageId == missionStageId);
        if (stageIndex < 0)
        {
            throw new UmbralDomainException(
                "operational_hint_stage_not_in_flow",
                "Operational Hint must target a Session Stage in this LiveSession flow.",
                UmbralFailureCategory.NotFound);
        }

        var operationalHint = LiveSessionStageHint.Create(
            Guid.NewGuid(),
            content,
            isSolution: false,
            ConvertCoordinate(latitude, "operational_hint_latitude_invalid"),
            ConvertCoordinate(longitude, "operational_hint_longitude_invalid"));
        orderedStages[stageIndex] = orderedStages[stageIndex] with
        {
            Hints = orderedStages[stageIndex].Hints.Concat(new[] { operationalHint }).ToArray()
        };

        ReplaceSessionStageFlow(orderedStages);
        SequenceNumber++;

        return operationalHint;
    }

    public LiveSessionStage? GetCurrentStageForTeam(Guid sessionTeamId)
    {
        EnsureSessionTeamBelongsToLiveSession(sessionTeamId);

        var orderedStages = SessionStageFlow.OrderBy(stage => stage.SessionStageOrder).ToArray();
        if (orderedStages.Length == 0)
        {
            return null;
        }

        var progress = TeamProgressions.FirstOrDefault(existingProgress => existingProgress.SessionTeamId == sessionTeamId);
        if (progress is null)
        {
            return orderedStages[0];
        }

        if (string.Equals(progress.State, SessionTeamProgressStates.Completed, StringComparison.Ordinal))
        {
            return null;
        }

        return progress.CurrentStageIndex >= orderedStages.Length
            ? null
            : orderedStages[progress.CurrentStageIndex];
    }

    public string GetProgressStateForTeam(Guid sessionTeamId)
    {
        EnsureSessionTeamBelongsToLiveSession(sessionTeamId);

        var progress = TeamProgressions.FirstOrDefault(existingProgress => existingProgress.SessionTeamId == sessionTeamId);
        return progress?.State ?? SessionTeamProgressStates.NotStarted;
    }

    public bool IsStagePending(Guid missionStageId)
    {
        var orderedStages = GetOrderedStages();
        var stageIndex = Array.FindIndex(orderedStages, stage => stage.MissionStageId == missionStageId);
        if (stageIndex < 0)
        {
            throw new UmbralDomainException(
                "session_stage_not_in_flow",
                "Session Stage is not part of this LiveSession flow.",
                UmbralFailureCategory.NotFound);
        }

        return !TeamProgressions.Any(progress =>
            string.Equals(progress.State, SessionTeamProgressStates.Completed, StringComparison.Ordinal)
            || progress.CurrentStageIndex > stageIndex);
    }

    public void DeactivateStage(Guid missionStageId, DateTimeOffset updatedAtUtc)
    {
        EnsureStageDeactivationAllowed();

        var orderedStages = GetOrderedStages();
        if (orderedStages.Length <= 1)
        {
            throw new UmbralDomainException(
                "session_stage_flow_last_pending_stage",
                "Session Stage Flow must keep at least one pending Session Stage.",
                UmbralFailureCategory.Conflict);
        }

        var removedStageIndex = Array.FindIndex(orderedStages, stage => stage.MissionStageId == missionStageId);
        if (removedStageIndex < 0)
        {
            throw new UmbralDomainException(
                "session_stage_not_in_flow",
                "Session Stage is not part of this LiveSession flow.",
                UmbralFailureCategory.NotFound);
        }

        if (!IsStagePending(missionStageId))
        {
            throw new UmbralDomainException(
                "session_stage_not_pending",
                "Only pending Session Stages can be deactivated.",
                UmbralFailureCategory.Conflict);
        }

        var remainingStages = orderedStages
            .Where(stage => stage.MissionStageId != missionStageId)
            .Select((stage, index) => stage with { SessionStageOrder = index + 1 })
            .ToArray();
        var hasRemainingPendingStage = remainingStages.Any(stage =>
        {
            var originalStageIndex = Array.FindIndex(orderedStages, orderedStage => orderedStage.MissionStageId == stage.MissionStageId);
            return !TeamProgressions.Any(progress =>
                string.Equals(progress.State, SessionTeamProgressStates.Completed, StringComparison.Ordinal)
                || progress.CurrentStageIndex > originalStageIndex);
        });
        if (!hasRemainingPendingStage)
        {
            throw new UmbralDomainException(
                "session_stage_flow_last_pending_stage",
                "Session Stage Flow must keep at least one pending Session Stage.",
                UmbralFailureCategory.Conflict);
        }

        ReplaceSessionStageFlow(remainingStages);
        RecalculateTeamProgressionsAfterStageDeactivation(removedStageIndex, remainingStages.Length, updatedAtUtc);
        SequenceNumber++;
    }

    private LiveSessionStage[] GetOrderedStages()
    {
        var orderedStages = SessionStageFlow.OrderBy(stage => stage.SessionStageOrder).ToArray();
        if (orderedStages.Length == 0)
        {
            throw new UmbralDomainException(
                "live_session_stage_flow_required",
                "LiveSession must include at least one active Session Stage.",
                UmbralFailureCategory.Validation);
        }

        return orderedStages;
    }

    private void RecalculateTeamProgressionsAfterStageDeactivation(
        int removedStageIndex,
        int remainingStageCount,
        DateTimeOffset updatedAtUtc)
    {
        foreach (var progress in TeamProgressions)
        {
            if (string.Equals(progress.State, SessionTeamProgressStates.Completed, StringComparison.Ordinal))
            {
                continue;
            }

            if (progress.CurrentStageIndex > removedStageIndex)
            {
                progress.AdvanceTo(progress.CurrentStageIndex - 1, updatedAtUtc);
                continue;
            }

            if (progress.CurrentStageIndex != removedStageIndex)
            {
                continue;
            }

            if (removedStageIndex < remainingStageCount)
            {
                progress.AdvanceTo(removedStageIndex, updatedAtUtc);
                continue;
            }

            progress.Complete(updatedAtUtc);
        }
    }

    private static void EnsureTeamProgressAcceptsSubmission(SessionTeamProgress progress)
    {
        if (!string.Equals(progress.State, SessionTeamProgressStates.Completed, StringComparison.Ordinal))
        {
            return;
        }

        throw new UmbralDomainException(
            "session_team_progress_already_completed",
            "Session Team already completed the Session Stage Flow.",
            UmbralFailureCategory.Conflict);
    }

    private void EnsureStageNotAcceptedByTeam(Guid sessionTeamId, Guid missionStageId)
    {
        if (!HasAcceptedSubmissionForStage(sessionTeamId, missionStageId, excludingSubmissionId: null))
        {
            return;
        }

        throw new UmbralDomainException(
            "session_stage_already_resolved_by_team",
            "Session Team already resolved this Session Stage.",
            UmbralFailureCategory.Conflict);
    }

    private bool HasAcceptedSubmissionForStage(Guid sessionTeamId, Guid missionStageId, Guid? excludingSubmissionId)
        => EvidenceSubmissions.Any(submission =>
            submission.Id != excludingSubmissionId
            && submission.SessionTeamId == sessionTeamId
            && submission.MissionStageId == missionStageId
            && submission.Outcome == ValidationOutcome.Accepted);

    private void AcceptCurrentStage(
        SessionTeamProgress progress,
        IReadOnlyList<LiveSessionStage> orderedStages,
        DateTimeOffset acceptedAtUtc)
    {
        if (progress.CurrentStageIndex >= orderedStages.Count - 1)
        {
            progress.Complete(acceptedAtUtc);
            State = LiveSessionState.Finalized;
            return;
        }

        progress.AdvanceTo(progress.CurrentStageIndex + 1, acceptedAtUtc);
    }

    private void TryAcceptOverriddenStage(EvidenceSubmission evidenceSubmission, DateTimeOffset acceptedAtUtc)
    {
        if (HasAcceptedSubmissionForStage(
            evidenceSubmission.SessionTeamId,
            evidenceSubmission.MissionStageId,
            evidenceSubmission.Id))
        {
            return;
        }

        var orderedStages = GetOrderedStages();
        var stageIndex = Array.FindIndex(
            orderedStages,
            stage => stage.MissionStageId == evidenceSubmission.MissionStageId);
        if (stageIndex < 0)
        {
            throw new UmbralDomainException(
                "validation_override_stage_not_in_flow",
                "Validation Override references a Session Stage outside the Session Stage Flow.",
                UmbralFailureCategory.Conflict);
        }

        var progress = GetOrCreateProgress(evidenceSubmission.SessionTeamId, acceptedAtUtc);
        if (string.Equals(progress.State, SessionTeamProgressStates.Completed, StringComparison.Ordinal)
            || progress.CurrentStageIndex > stageIndex)
        {
            return;
        }

        if (progress.CurrentStageIndex < stageIndex)
        {
            throw new UmbralDomainException(
                "validation_override_stage_not_current",
                "Validation Override cannot advance a future Session Stage.",
                UmbralFailureCategory.Conflict);
        }

        AcceptCurrentStage(progress, orderedStages, acceptedAtUtc);
    }

    private void RaiseEvidenceSubmittedEvents(
        EvidenceSubmission evidenceSubmission,
        string source,
        DateTimeOffset submittedAtUtc)
    {
        RaiseDomainEvent(new EvidenceSubmittedDomainEvent(
            Id,
            evidenceSubmission.Id,
            evidenceSubmission.SessionTeamId,
            evidenceSubmission.MissionStageId,
            evidenceSubmission.GameType,
            submittedAtUtc));
        RaiseDomainEvent(new EvidenceValidatedDomainEvent(
            Id,
            evidenceSubmission.Id,
            evidenceSubmission.SessionTeamId,
            evidenceSubmission.MissionStageId,
            evidenceSubmission.Outcome.ToString(),
            source,
            submittedAtUtc));
    }

    private void EnsureEvidenceSubmissionAllowed()
    {
        if (State == LiveSessionState.Paused
            || State == LiveSessionState.Finalized
            || State == LiveSessionState.Canceled)
        {
            throw new UmbralDomainException(
                "live_session_not_accepting_evidence",
                "LiveSession is not accepting Evidence Submissions.",
                UmbralFailureCategory.Conflict);
        }
    }

    private void EnsureHintReleaseAllowed()
    {
        if (State == LiveSessionState.Active
            || State == LiveSessionState.Paused)
        {
            return;
        }

        throw new UmbralDomainException(
            "live_session_not_accepting_hint_release",
            "LiveSession must be Active or Paused to release or create Hints.",
            UmbralFailureCategory.Conflict);
    }

    private void EnsureStageDeactivationAllowed()
    {
        if (State == LiveSessionState.Scheduled
            || State == LiveSessionState.Active
            || State == LiveSessionState.Paused)
        {
            return;
        }

        throw new UmbralDomainException(
            "live_session_stage_deactivation_not_allowed_for_state",
            "LiveSession must be Scheduled, Active or Paused to deactivate a pending Session Stage.",
            UmbralFailureCategory.Conflict);
    }

    private void EnsureSessionTeamBelongsToLiveSession(Guid sessionTeamId)
    {
        if (SessionTeams.Any(team => team.Id == sessionTeamId))
        {
            return;
        }

        throw new UmbralDomainException(
            "session_team_not_found",
            "Session Team does not belong to this LiveSession.",
            UmbralFailureCategory.NotFound);
    }

    private SessionTeamProgress GetOrCreateProgress(Guid sessionTeamId, DateTimeOffset startedAtUtc)
    {
        var existingProgress = TeamProgressions.FirstOrDefault(progress => progress.SessionTeamId == sessionTeamId);
        if (existingProgress is not null)
        {
            return existingProgress;
        }

        var progress = SessionTeamProgress.Create(Id, sessionTeamId, startedAtUtc);
        TeamProgressions.Add(progress);
        return progress;
    }

    private static LiveSessionStage SelectCurrentStage(
        SessionTeamProgress progress,
        IReadOnlyList<LiveSessionStage> orderedStages)
    {
        if (progress.CurrentStageIndex >= orderedStages.Count)
        {
            throw new UmbralDomainException(
                "session_team_progress_stage_index_invalid",
                "Session Team Progress points outside the Session Stage Flow.",
                UmbralFailureCategory.Conflict);
        }

        return orderedStages[progress.CurrentStageIndex];
    }

    private static void EnsureTreasureHuntStage(LiveSessionStage currentStage)
    {
        if (!string.Equals(currentStage.GameType, "TreasureHunt", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(currentStage.GameType, "Treasure Hunt", StringComparison.OrdinalIgnoreCase))
        {
            throw new UmbralDomainException(
                "evidence_submission_stage_not_treasure_hunt",
                "Current Session Stage does not accept Treasure Hunt QR submissions.",
                UmbralFailureCategory.Conflict);
        }

        if (!string.IsNullOrWhiteSpace(currentStage.ExpectedQrHash))
        {
            return;
        }

        throw new UmbralDomainException(
            "evidence_submission_expected_qr_hash_required",
            "Current Session Stage does not define an expected QR hash.",
            UmbralFailureCategory.Validation);
    }

    private static void EnsureTriviaStage(LiveSessionStage currentStage, Guid selectedChoiceId)
    {
        if (!string.Equals(currentStage.GameType, "Trivia", StringComparison.OrdinalIgnoreCase))
        {
            throw new UmbralDomainException(
                "evidence_submission_stage_not_trivia",
                "Current Session Stage does not accept Trivia answer submissions.",
                UmbralFailureCategory.Conflict);
        }

        if (!currentStage.CorrectChoiceId.HasValue)
        {
            throw new UmbralDomainException(
                "evidence_submission_trivia_answer_required",
                "Current Session Stage does not define a valid Trivia answer.",
                UmbralFailureCategory.Validation);
        }

        if (currentStage.Choices.All(choice => choice.Id != selectedChoiceId))
        {
            throw new UmbralDomainException(
                "evidence_submission_choice_not_in_play",
                "Selected choice is not one of the Play choices.",
                UmbralFailureCategory.Validation);
        }
    }

    private void EnsureEnrollmentAllowed(JoinCode presentedJoinCode, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(presentedJoinCode);
        EnsureScheduled();

        if (JoinCodeValue is null)
        {
            throw new UmbralDomainException(
                "live_session_join_code_not_generated",
                "LiveSession does not have a Join Code generated.",
                UmbralFailureCategory.Validation);
        }

        if (!string.Equals(JoinCodeValue, presentedJoinCode.Value, StringComparison.Ordinal))
        {
            throw new UmbralDomainException(
                "join_code_invalid_for_live_session",
                "Join Code is invalid for this LiveSession.",
                UmbralFailureCategory.NotFound);
        }

        if (!EnrollmentWindow.IsOpenAt(nowUtc))
        {
            throw new UmbralDomainException(
                "live_session_enrollment_window_not_active",
                "Enrollment window is not active.",
                UmbralFailureCategory.Conflict);
        }
    }

    private void EnsureScheduled()
    {
        EnsureState(
            LiveSessionState.Scheduled,
            "live_session_not_scheduled",
            "LiveSession must be scheduled to accept enrollment changes.");
    }

    private void EnsureState(LiveSessionState expectedState, string errorCode, string errorMessage)
        => EnsureState([expectedState], errorCode, errorMessage);

    private void EnsureState(
        LiveSessionState expectedStateA,
        LiveSessionState expectedStateB,
        string errorCode,
        string errorMessage)
        => EnsureState([expectedStateA, expectedStateB], errorCode, errorMessage);

    private void EnsureState(
        LiveSessionState expectedStateA,
        LiveSessionState expectedStateB,
        LiveSessionState expectedStateC,
        string errorCode,
        string errorMessage)
        => EnsureState([expectedStateA, expectedStateB, expectedStateC], errorCode, errorMessage);

    private void EnsureState(
        IReadOnlyCollection<LiveSessionState> expectedStates,
        string errorCode,
        string errorMessage)
    {
        if (expectedStates.Contains(State))
        {
            return;
        }

        throw new UmbralDomainException(errorCode, errorMessage, UmbralFailureCategory.Conflict);
    }

    private static Guid NormalizeGuid(Guid value, string errorCode, string errorMessage)
    {
        if (value == Guid.Empty)
        {
            throw new UmbralDomainException(errorCode, errorMessage, UmbralFailureCategory.Validation);
        }

        return value;
    }

    private static string NormalizeRequiredText(
        string value,
        string errorCode,
        string errorMessage,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new UmbralDomainException(errorCode, errorMessage, UmbralFailureCategory.Validation);
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new UmbralDomainException(
                $"{errorCode}_too_long",
                $"Value cannot exceed {maximumLength} characters.",
                UmbralFailureCategory.Validation);
        }

        return normalized;
    }

    private static decimal? ConvertCoordinate(double? value, string errorCode)
    {
        if (!value.HasValue)
        {
            return null;
        }

        if (double.IsNaN(value.Value) || double.IsInfinity(value.Value))
        {
            throw new UmbralDomainException(
                errorCode,
                "Operational Hint coordinates must be finite numbers.",
                UmbralFailureCategory.Validation);
        }

        return (decimal)value.Value;
    }

    private static IReadOnlyList<LiveSessionStage> NormalizeSessionStageFlow(IReadOnlyList<LiveSessionStage> sessionStageFlow)
    {
        if (sessionStageFlow.Count == 0)
        {
            throw new UmbralDomainException(
                "live_session_stage_flow_required",
                "LiveSession must include at least one active Session Stage.",
                UmbralFailureCategory.Validation);
        }

        var duplicateMissionStageId = sessionStageFlow
            .GroupBy(stage => stage.MissionStageId)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateMissionStageId is not null)
        {
            throw new UmbralDomainException(
                "live_session_stage_flow_duplicate_stage",
                $"Mission Stage '{duplicateMissionStageId.Key}' cannot appear twice in Session Stage Flow.",
                UmbralFailureCategory.Validation);
        }

        var orderedStages = sessionStageFlow
            .OrderBy(stage => stage.SessionStageOrder)
            .ToArray();

        for (var index = 0; index < orderedStages.Length; index++)
        {
            if (orderedStages[index].SessionStageOrder != index + 1)
            {
                throw new UmbralDomainException(
                    "live_session_stage_flow_order_invalid",
                    "Session Stage Flow order must be contiguous and start at one.",
                    UmbralFailureCategory.Validation);
            }
        }

        return orderedStages;
    }
}
