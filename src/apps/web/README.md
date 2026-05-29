## Web

Next.js shell for `Administrator` and `Operator`.

### Scope

- Keycloak login and logout
- protected routes for web roles
- browser API checks with attached JWT
- SignalR reconnect hook with resync callback
- calm operational layout aligned with `docs/product/design.md`

### Local run with Docker Compose

Install frontend deps:

```bash
docker compose --env-file .env.example -f docker-compose.dev.yml -f docker-compose.utils.yml run --rm web-package-manager install next react react-dom @microsoft/signalr @fontsource/ibm-plex-sans @fontsource/ibm-plex-mono
docker compose --env-file .env.example -f docker-compose.dev.yml -f docker-compose.utils.yml run --rm web-package-manager install -D typescript @types/node @types/react @types/react-dom eslint eslint-config-next @eslint/eslintrc
```

Start backend stack plus web shell:

```bash
docker compose --env-file .env.example -f docker-compose.dev.yml up --build web edge-proxy keycloak mission-design-service session-operations-service scoring-audit-service
```

Run lint:

```bash
docker compose --env-file .env.example -f docker-compose.dev.yml -f docker-compose.utils.yml run --rm web-package-manager run lint
```

### Verification

1. Open `http://localhost:3000/login`
2. Login with `admin / admin123!`
3. Verify redirect to `/administrator`
4. Login with `operator / operator123!`
5. Verify redirect to `/operator`
6. Login with `participant / participant123!`
7. Verify `403` role rejection message
8. Stop `session-operations-service` or `edge-proxy`
9. Verify SignalR state flips to error or reconnecting
10. Bring service back and verify resync counter increments
