# Mobile

Expo shell for `Participant`.

## Scope

- Keycloak login and logout for the mobile client
- protected participant routes
- edge proxy API client with attached `JWT`
- SignalR reconnect hook with resync callback
- guided mobile navigation aligned with `docs/product/design.md`

## Compose setup

1. Use the root `.env.example` as the compose env file, or copy it to `.env`.
2. Install mobile dependencies through compose:

```bash
docker compose --env-file .env.example -f docker-compose.dev.yml -f docker-compose.utils.yml run --rm mobile-package-manager install
```

3. Run the mobile typecheck through compose:

```bash
docker compose --env-file .env.example -f docker-compose.dev.yml -f docker-compose.utils.yml run --rm mobile-package-manager run typecheck
```

4. Start the required local stack plus the Expo web shell:

```bash
docker compose --env-file .env.example -f docker-compose.dev.yml up -d edge-proxy mobile
```

The `mobile` service bootstraps missing `node_modules` before starting Expo web on `http://localhost:19006`, so `docker compose up` is enough even after a clean volume.

If you want a native phone preview instead of Expo web, see `README.expo-go.md`.

## Verification

1. Open `http://localhost:19006` after `docker compose ... up -d`.
2. Login with `participant / participant123!`.
3. Verify redirect into the participant shell.
4. Login with `operator / operator123!` or `admin / admin123!`.
5. Verify mobile role rejection and redirect to the forbidden screen.
6. On the game hub, confirm the connection chip and, if enrolled, the current stage card with its `Continuar misión` CTA.
7. Join a session by code, then open the board and confirm the stage `Prompt` is shown before submitting evidence.
8. For a `Trivia` stage submit a text answer; for a `Treasure Hunt` stage scan a QR. Verify immediate accepted/rejected feedback.
9. Open `Progreso` and confirm the guided stage path; open `Pistas` and confirm released hints (and revealed solutions once finalized).
10. Open `Ranking` and confirm live standings highlight your team.
11. Stop `session-management-service` or the network and verify the connection state moves to `error` or `reconnecting`, then restores on reconnect.

## Notes

- The participant client implements the full game flow: hub, session join, live board with `Trivia` text answers and `Treasure Hunt` QR scan, guided progress path, hints/solutions and live ranking.
- `User` identity stays in auth state; it is not treated as `Session Team`.
- The compose flow keeps dependency install, typecheck and runtime validation inside containers instead of local host commands.
