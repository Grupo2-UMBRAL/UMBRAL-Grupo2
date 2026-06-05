## Web

Next.js shell for `Administrator` and `Operator`.

### Scope

- Keycloak login and logout
- protected routes for web roles
- browser API checks with attached JWT
- administrator Operator User provisioning through `/identity-access/api/identity-access/operators` via edge-proxy
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
docker compose --env-file .env.example -f docker-compose.dev.yml up --build web edge-proxy keycloak identity-access-service mission-design-service session-operations-service scoring-audit-service
```

Run lint:

```bash
docker compose --env-file .env.example -f docker-compose.dev.yml -f docker-compose.utils.yml run --rm web-package-manager run lint
```

### Verification

1. Open `http://localhost:3000/login`
2. Login with `admin / admin123!`
3. Verify redirect to `/administrator`
4. Verify administrator shell can load Operator Users from `/identity-access/api/identity-access/operators`
5. Login with `operator / operator123!`
6. Verify redirect to `/operator`
7. Login with `participant / participant123!`
8. Verify `403` role rejection message
9. Verify browser API calls route through `edge-proxy`, including `/identity-access/*` once the service project exists in the worktree
10. Stop `session-operations-service` or `edge-proxy`
11. Verify SignalR state flips to error or reconnecting
12. Bring service back and verify resync counter increments
