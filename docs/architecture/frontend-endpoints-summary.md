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
- Login hace exchange contra Keycloak y guarda la sesión web.

## Identity and Access

Base: `/identity-access/api/identity-access`

- `GET /bootstrap`
- `GET /operators`
- `POST /operators`
- `POST /operators/{userId}/deactivate`
- `POST /operators/{userId}/reset-password`

## Mission Design

Base: `/mission-design/api/mission-design`

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

Base: `/session-operations/api/session-operations`

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

### Operación de envíos

- `POST /submissions/{submissionId}/override`

### Flujos de participante no usados hoy

- `GET /session-enrollment/{joinCode}/validate`
- `GET /session-enrollment/{joinCode}/teams`
- `POST /session-enrollment/teams`
- `POST /session-enrollment/join`
- `GET /session-teams/{sessionTeamId}/snapshot`

## Scoring and Audit

Base: `/scoring-audit/api/scoring-audit`

- `GET /bootstrap`
- `POST /sessions/{liveSessionId}/scores`
- `POST /sessions/{liveSessionId}/penalties`
- `GET /sessions/{liveSessionId}/ranking`
- `GET /sessions/{liveSessionId}/event-log`
- `POST /sessions/{liveSessionId}/event-log`

## Realtime

- Session hub: `/session-hub/hubs/session`
- Scoring hub: `/scoring-audit/hub/scoring`

## Smoke checks

- `GET /mission-design/api/mission-design/smoke/{administrator|operator}`
- `GET /session-operations/api/session-operations/smoke/{administrator|operator}`
- `GET /scoring-audit/api/scoring-audit/smoke/{administrator|operator}`
