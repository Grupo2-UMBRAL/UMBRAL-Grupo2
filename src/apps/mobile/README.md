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
6. Open the home screen and tap `Probe edge proxy`.
7. Verify the response shows `200` from `/health` and the outgoing request preview includes `Authorization: Bearer ...`.
8. Stop `edge-proxy` and verify the probe fails cleanly.
9. With a valid participant session, observe the SignalR connection state on the home screen.
10. Stop `session-management-service` or the network and verify the state moves to `error` or `reconnecting`.
11. Restore connectivity and verify the resync counter increments once the hub reconnects.

## Notes

- The shell intentionally stops at auth, transport and placeholder navigation. It does not implement QR scan, live team board or ranking logic yet.
- `User` identity stays in auth state; it is not treated as `Session Team`.
- The compose flow keeps dependency install, typecheck and runtime validation inside containers instead of local host commands.
