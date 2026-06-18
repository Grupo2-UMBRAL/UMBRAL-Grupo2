# UMB-17 Mobile Team Board Verification

## Preconditions

- Backend `session-management-service` is running through edge proxy.
- Participant user is authenticated in mobile app with role `Participant`.
- Participant completed UMB-33 Join Flow and AsyncStorage contains `{ joinCode, teamId, teamName }`.
- UMB-34 endpoint is available:
  - `GET /api/session-management/session-teams/{sessionTeamId}/snapshot`
- UMB-34 SignalR hub is available through configured `EXPO_PUBLIC_SESSION_OPERATIONS_HUB_PATH`.

## Automated Evidence

Run from `src/apps/mobile`:

```powershell
npm run test
npm run typecheck
```

Expected:

- Jest suites pass: `3/3`.
- Jest tests pass: `13/13`.
- TypeScript passes with `tsc --noEmit`.
- Existing Jest warning about `expo-router/babel` can appear; it is config noise and does not fail tests.

## Manual: Snapshot Render

1. Launch mobile app and sign in as Participant.
2. Complete join/enrollment flow for a valid Session Team.
3. Navigate to `Team board`.
4. Expected result:
   - App calls `GET /api/session-management/session-teams/{sessionTeamId}/snapshot`.
   - Board shows Session Team name, Session State, Progress State and Sync metadata.
   - Board shows current playable stage if snapshot includes `currentStage`.
   - Board shows only hints whose `missionStageId` matches current stage.

## Manual: SignalR Incremental Hint

1. Keep participant board open.
2. From Operator/backend, unlock a hint for same Session Team and current stage.
3. Expected result:
   - Client receives `ReceiveHintUnlocked`.
   - New hint appears in `Visible hints` without full page reload.
   - Duplicate `hintId` does not duplicate UI item.

## Manual: Timer And Session State

1. Keep participant board open while LiveSession changes state.
2. Trigger session state update from backend/operator.
3. Expected result:
   - Client receives `ReceiveSessionStateChanged`.
   - Board updates Session State chip.
   - If payload includes `remainingSeconds`, clock renders `MM:SS` and counts down locally.

## Manual: Backend Disconnect And Resync

1. Start stack normally:

```powershell
docker compose -f docker-compose.dev.yml up -d
```

2. Launch mobile app and open `Team board` until status shows `Sincronizado`.
3. Stop Session Operations only:

```powershell
docker compose -f docker-compose.dev.yml stop session-management-service
```

4. Expected result while stopped:
   - SignalR state moves to `Reconectando` or `Sin conexión`.
   - Board keeps last snapshot visible and marks data as stale/error only if resync fails.
5. Start Session Operations again:

```powershell
docker compose -f docker-compose.dev.yml start session-management-service
```

6. Expected result after reconnect:
   - SignalR `onResync` fires.
   - App calls snapshot HTTP endpoint again.
   - If returned `sync.sequenceNumber` is newer or equal, board replaces local snapshot.
   - Status returns to `Sincronizado` / `Datos frescos`.

## Manual: Full Refresh Policy

1. Emit or trigger SignalR payload with `refreshPolicy = RefreshSnapshot`.
2. Expected result:
   - Board does not apply partial update blindly.
   - Board calls full snapshot HTTP endpoint.
   - Board applies snapshot only when `sync.sequenceNumber >= currentSequence`.

## Notes

- Board intentionally does not implement QR scan, Evidence Submission, ranking or backend snapshot logic.
- Snapshot sequence starts at backend-provided value; client uses it only to avoid applying stale events.
- If `VisibleHints` is empty, UI remains stable and waits for `ReceiveHintUnlocked` or full resync.
