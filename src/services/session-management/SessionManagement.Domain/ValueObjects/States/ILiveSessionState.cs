using System;
using SessionManagement.Domain.Abstractions;
using Umbral.ServiceDefaults;

namespace SessionManagement.Domain.LiveSessions.States;

public interface ILiveSessionState
{
    string Name { get; }

    void Start(LiveSession session, DateTimeOffset startedAtUtc);
    
    void Pause(LiveSession session);
    
    void Resume(LiveSession session);
    
    void FinalizeSession(LiveSession session);
    
    void Cancel(LiveSession session);

    void EnsureCanModifyEnrollment();
    
    void EnsureCanSubmitEvidence();
    
    void EnsureCanReleaseHint();
    
    void EnsureCanDeactivateStage();
    
    void EnsureCanPenalize();
}
