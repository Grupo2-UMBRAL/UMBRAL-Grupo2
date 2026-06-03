# UMB-33 Mobile Join Flow Verification

## Preconditions

- Backend `session-operations` is running through the edge proxy.
- Participant user is authenticated in mobile app with role `Participant`.
- UMB-40 endpoints are available:
  - `GET /api/session-operations/session-enrollment/{joinCode}/validate`
  - `GET /api/session-operations/session-enrollment/{joinCode}/teams`
  - `POST /api/session-operations/session-enrollment/join`
  - `POST /api/session-operations/session-enrollment/teams`
- Operator has generated a Session Join Code and opened Team Assignment Window for target LiveSession.

## Automated Evidence

Run from `src/apps/mobile`:

```powershell
npm run test
npm run test -- --coverage --coverageReporters=text-summary --coverageReporters=lcov --runInBand
npm run typecheck
```

Expected:

- Jest suites pass: `2/2`.
- Jest tests pass: `7/7`.
- Coverage summary: statements `73.56%`, branches `54.03%`, functions `84.09%`, lines `73.41%`.
- TypeScript passes with `tsc --noEmit`.

## Manual: Successful Join Existing Team

1. Launch mobile app and sign in as Participant.
2. Navigate to `Join session`.
3. Enter valid Session Join Code.
4. Confirm status chip changes to `open`.
5. Confirm existing Session Teams appear.
6. Tap a Session Team.
7. Tap `Join selected team`.
8. Expected result:
   - App calls `POST /api/session-operations/session-enrollment/join` with `{ joinCode, sessionTeamId }`.
   - App stores `{ joinCode, teamId, teamName }` in AsyncStorage.
   - App redirects to `/board`.
   - Board displays restored Team context.

## Manual: Successful Create Team

1. Launch mobile app and sign in as Participant.
2. Navigate to `Join session`.
3. Enter valid Session Join Code with open Team Assignment Window.
4. Tap `Create team`.
5. Enter a Session Team name with at least 3 characters.
6. Tap `Create Session Team`.
7. Expected result:
   - App calls `POST /api/session-operations/session-enrollment/teams` with `{ joinCode, teamName }`.
   - App stores `{ joinCode, teamId, teamName }` in AsyncStorage.
   - App redirects to `/board`.

## Manual: Failed Join Code

1. Navigate to `Join session`.
2. Enter invalid Session Join Code.
3. Expected result:
   - App displays `Session Join Code inválido o no registrado.`
   - Team list is not fetched.
   - Join/create actions are unavailable.

## Manual: Closed Team Assignment Window

1. Enter valid Session Join Code for a LiveSession whose Team Assignment Window is closed.
2. Expected result:
   - App shows warning: `Team Assignment Window está cerrada. No puedes crear ni unirte a equipos ahora.`
   - Existing-team join and create-team CTAs are unavailable.
   - No enrollment context is stored.

## Manual: Reopen App / Stored Enrollment

1. Complete successful join or create flow.
2. Close the app.
3. Reopen app with same authenticated Participant session.
4. Navigate to `Join session`.
5. Expected result:
   - App detects stored enrollment in AsyncStorage.
   - App redirects automatically to `/board`.
   - Board restores Join Code, Team name and Team ID.

## Manual: Simulated Network Loss

1. Launch app and navigate to `Join session`.
2. Disable network or stop edge proxy.
3. Enter valid-looking Session Join Code.
4. Expected result:
   - App displays `No se pudo conectar con Session Operations.` or a server error message.
   - Existing-team join and create-team actions remain unavailable until validation succeeds.
5. Restore network and re-enter Join Code.
6. Expected result:
   - App validates Join Code and resumes normal open/closed-window behavior.

## Notes

- Sign-out clears stored enrollment to avoid reusing Team context between users.
- If auth session expires during hydration, stored enrollment is cleared with stored auth session.
- `expo-router/babel` deprecation warning appears during Jest; it is pre-existing config noise and does not fail tests.
