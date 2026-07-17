# Front Endpoints Summary

Resumen operativo de las rutas que consume hoy el front web y que sirven como base para rehacerlo en Vite + React puro.

## Base

- El front habla con `edge-proxy` por defecto en `http://localhost:7500`.
- Las rutas protegidas requieren `Bearer JWT`.
- `Administrator` y `Operator` usan la consola web.
- `Participant` no tiene flujos web en el shell actual.

## Auth shell

- `POST /api/auth/login`
- `POST /api/auth/logout`
- Login hace exchange contra Keycloak y guarda la sesiÃ³n web.

## User Management

Base: `/user-management/api/user-management`

- `GET /bootstrap`
- `GET /operators`
- `POST /operators` — body `{ username, email }` only; Keycloak emails the operator an onboarding link to set their own password and complete their profile
- `POST /operators/{userId}/resend-onboarding-invitation`
- `POST /operators/{userId}/deactivate`
- `POST /operators/{userId}/reset-password`

## Mission Design

Base: `/mission-management/api/mission-management`

- `GET /bootstrap`
- `GET /missions`
- `GET /missions/{missionId}`
- `POST /missions`
- `PUT /missions/{missionId}`
- `POST /missions/{missionId}/activate`
- `POST /missions/{missionId}/deactivate`
- `GET /missions/{missionId}/stages`
- `POST /missions/{missionId}/stages`
- `GET /missions/eligible-for-live-session`
- `GET /missions/eligible-for-live-session/{missionId}`
- `GET /stages/{missionStageId}`
- `POST /stages/{missionStageId}/hints`
- `POST /stages/{missionStageId}/deactivate`

## Session Operations

Base: `/session-management/api/session-management`

- `GET /bootstrap`

### Live sessions

- `GET /live-sessions`
- `GET /live-sessions/{liveSessionId}`
- `GET /live-sessions/{liveSessionId}/overview`
- `GET /live-sessions/{liveSessionId}/teams/{sessionTeamId}/detail?inactivityThresholdMinutes=10`
- `POST /live-sessions`
- `POST /live-sessions/{liveSessionId}/lifecycle/start`
- `POST /live-sessions/{liveSessionId}/lifecycle/pause`
- `POST /live-sessions/{liveSessionId}/lifecycle/resume`
- `POST /live-sessions/{liveSessionId}/lifecycle/finalize`
- `POST /live-sessions/{liveSessionId}/lifecycle/cancel`
- `POST /live-sessions/{liveSessionId}/penalties`
- `POST /live-sessions/{liveSessionId}/stages/{missionStageId}/deactivate`
- `POST /live-sessions/{liveSessionId}/stages/{missionStageId}/hints`
- `POST /live-sessions/{liveSessionId}/hints/{hintId}/release`

### OperaciÃ³n de envÃ­os

- `POST /submissions/{submissionId}/override`

### Flujos de participante no usados hoy

- `GET /session-enrollment/{joinCode}/validate`
- `GET /session-enrollment/{joinCode}/teams`
- `POST /session-enrollment/teams`
- `POST /session-enrollment/join`
- `GET /session-teams/{sessionTeamId}/snapshot`

## Scoring and Monitoring

Base: `/scoring-monitoring/api/scoring-monitoring`

- `GET /bootstrap`
- `POST /sessions/{liveSessionId}/scores`
- `POST /sessions/{liveSessionId}/penalties`
- `GET /sessions/{liveSessionId}/ranking`
- `GET /sessions/{liveSessionId}/event-log`
- `POST /sessions/{liveSessionId}/event-log`

## Realtime

- Session hub: `/session-hub/hubs/session`
- Scoring hub: `/scoring-monitoring/hub/scoring`

## Smoke checks

- `GET /mission-management/api/mission-management/smoke/{administrator|operator}`
- `GET /session-management/api/session-management/smoke/{administrator|operator}`
- `GET /scoring-monitoring/api/scoring-monitoring/smoke/{administrator|operator}`
