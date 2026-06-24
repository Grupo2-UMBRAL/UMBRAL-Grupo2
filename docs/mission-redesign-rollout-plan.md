# Mission Redesign — Plan de adaptacion (cross-context + apps)

Plan para propagar el rediseno de `mission-management` (ver `src/services/mission-management/docs/adr/0001-linear-path-challenge-model.md`) al resto del sistema: session, scoring, identidad, web y mobile. La base de datos es descartable (pre-release): sin migracion de datos.

## 0. Decision de integracion (linchpin)

**`mission-management` hace el flatten.** El endpoint `GET /api/mission-management/missions/eligible-for-live-session/{id}` ya devuelve HOY una lista plana de stages hoja (aplana el arbol). En el modelo nuevo devuelve una lista **plana y ordenada de plays** (Questions/Searches), no Challenges ni Sections.

Consecuencia: la recursion (Section) y la agrupacion (Challenge) son **solo de autoria**. El runtime (session/scoring) sigue siendo una **secuencia plana**. Por eso el ripple es chico:
- session/scoring solo **renombran** `stage -> play` + Trivia pasa de texto a **choice**.
- No necesitan conocer Section, Challenge ni recursion.

Alternativa rechazada: devolver Challenges y que session aplane -> mas ripple, mete arbol en el runtime sin beneficio.

## 1. mission-management (owner) — que debe exponer

Reemplazar `EligibleMissionStageSnapshot` por `EligiblePlaySnapshot` (lista plana, orden global del flatten):

```
EligiblePlaySnapshot {
  Id              // id de la play (Question/Search) = atomo puntuado
  Order           // orden global del flatten depth-first
  GameType        // "Trivia" | "Treasure Hunt"
  Difficulty      // resuelto: play.override ?? challenge.default
  TimeLimitMinutes// resuelto: play.override ?? challenge.default
  Prompt          // Question.Text  (Trivia)  |  Search.Clue (Treasure Hunt)
  // Trivia:
  Choices[]       // { Id, Text }   (2..4)   SIN marcar la correcta
  CorrectChoiceId // server-to-server: lo usa session para validar
  // Treasure Hunt:
  ExpectedQrHash
  Hints[]         // { Content, IsSolution, Lat?, Long? }
}
```

Regla critica: `CorrectChoiceId` viaja **server-to-server** (a session). **Nunca** se expone al jugador.

## 2. session-management — change list

Estrategia: mantener el flujo plano (`SessionStageFlow`), renombrar a **play**, cambiar Trivia a choice. Snapshot del mission al crear la sesion sigue igual (copia local).

**(a) Contrato de ingreso**
- `SessionManagement.Infrastructure/MissionManagementLiveSessionCatalog.cs:11` — consume el endpoint; sin cambio de URL, cambia el shape a `EligiblePlaySnapshot`.
- `SessionManagement.Application/Features/LiveSessions/LiveSessionContracts.cs:47` — `EligibleMissionStageSnapshot -> EligiblePlaySnapshot`: quitar `TriviaValidAnswer`/`TriviaInitialValidationCriterion`, agregar `Choices[]` + `CorrectChoiceId`.
- `LiveSessionContracts.cs:113` (`ToDomain`) — mapear choices + correctChoiceId.

**(b) Granularidad stage -> play (plano, sin recursion)**
- `SessionManagement.Domain/LiveSessions/LiveSessionStage.cs:5` — renombrar a `LiveSessionPlay` (o documentar). Cada item de la lista plana ya es una play; **no** se agrega coleccion de questions/searches (el flatten viene hecho).
- `LiveSession.cs:53` `SessionStageFlowJson` y `:107` `ReplaceSessionStageFlow` — siguen igual (lista plana ordenada), solo renombre semantico a "play flow".

**(c) Validacion Trivia: texto -> choiceId**
- `LiveSession.cs:411` `SubmitTriviaAnswer` — firma pasa de `string answerText` a `Guid selectedChoiceId`.
- `LiveSession.cs:429` — reemplazar comparacion de texto por `accepted = currentPlay.CorrectChoiceId == selectedChoiceId`.
- `LiveSession.cs:946` — borrar `NormalizeTriviaAnswerForComparison`.
- `LiveSession.cs:935` `EnsureTriviaStage` — validar `CorrectChoiceId.HasValue` y que `selectedChoiceId` este entre los choices.

**(d) Evidencia (payload mobile -> backend)**
- `SessionManagement.Domain/LiveSessions/EvidenceSubmission.cs:5` — agregar `SubmittedChoiceId: Guid?`; `CreateTrivia` recibe `choiceId` (no texto). `SubmittedText` se puede borrar (DB descartable). QR (`SubmittedHash`) sin cambio.
- `.../SubmitTriviaAnswer/SubmitTriviaAnswerCommand.cs:5` — `AnswerText -> SelectedChoiceId: Guid` (command + request).
- Handler `SubmitTriviaAnswerCommandHandler` — pasar `SelectedChoiceId`.

**(e) Snapshot al jugador (ocultar la correcta)**
- El contrato que ve el jugador (equivalente backend de `CurrentSessionStageSnapshot`) debe incluir `Choices[] { Id, Text }` **sin** `CorrectChoiceId` ni flag de correcta. Validacion 100% server-side.

**(f) Llamada a scoring** — sin cambio de logica, solo el id que se manda es la play:
- `SessionManagement.Application/Scoring/IScoringAuditClient.cs:20` `RecordStageCreditRequest.MissionStageId` pasa a referir la **play id**. Difficulty + ResolutionTime igual.

**(g) Vocabulario** — `src/services/session-management/CONTEXT.md`: "Session Stage Flow" -> "Session Flow" (lista de Plays); "Mission Stage" -> "Play"; "session" (LiveSession/SessionTeam) se mantiene; aclarar Trivia valida por choice.

## 3. scoring-monitoring — solo rename (logica intacta)

La matematica no cambia (Easy=100, Medium=200, Hard=300; dedupe single-credit; ResolutionTime para desempate). Solo nombres:
- `ScoreEntry.cs:61` `MissionStageId -> PlayId`.
- `Scoreboard.cs:204` `StageCreditKey -> PlayCreditKey`; `:59` `GrantStageCredit -> GrantPlayCredit`; `:196` `ScoreFor` igual.
- `MissionStageDifficulty -> PlayDifficulty`.
- `RecordStageCredit{Command,Request,Response}.cs` — `MissionStageId -> PlayId`.
- `RecordStageCreditCommandHandler.cs:99` — texto log "Mission Stage" -> "Play".
- `ScoringMonitoringDbContext.cs:78,102` — columna + indice `MissionStageId -> PlayId` (nueva migracion, DB descartable).
- `src/services/scoring-monitoring/CONTEXT.md:11,20,28,63` — "Mission Stage" -> "Play", "Stage Credit" -> "Play Credit", "Single-Stage Credit" -> "Single-Play Credit".

## 4. identidad / user-management — sin impacto

Sin `CONTEXT.md`, cero referencias a mission/stage. La autorizacion por rol (Administrator/Operator/Participant) es independiente de la estructura. No requiere cambios.

## 5. Vocabulario a adaptar (old -> new)

**Principio.** El atomo que cruza los contextos tiene UN solo nombre: **Play** = la resolucion en runtime de una `Question` o `Search` autoreada. El viejo `Mission Stage` / `stage` (la hoja del arbol) se **retira en todos lados**. Clave: **"session" NO se toca** — solo cambia la UNIDAD (`stage -> play`) y su flujo. La estructura del runtime sigue siendo "session".

### Que cambia

| Contexto | Antes | Ahora |
|---|---|---|
| mission-management | Mission Node / Mission Stage / Substage / Default Time Budget | Section / Challenge / **Play** (= `Question` \| `Search`) / Choice / Hint / Time Limit |
| session-management | Mission Stage / Session Stage / Session Stage Flow / `MissionStageId` | **Play** / **Session Flow** / `PlayId` |
| scoring-monitoring | Mission Stage / Stage Credit / Single-Stage Credit / `MissionStageId` / `StageCreditKey` / `MissionStageDifficulty` | Play / **Play Credit** / Single-Play Credit / `PlayId` / `PlayCreditKey` / `PlayDifficulty` |
| identidad / user-management | — | (sin cambios) |
| `CONTEXT-MAP.md` | "Session Stage Flow" derivado del arbol | mission `Question`/`Search` -> (flatten) -> session **Play** -> scoring **Play Credit** |

### Que se MANTIENE en session (NO pasa a play)

"Session" es la estructura; "play" es solo el atomo que el equipo resuelve. Siguen igual, sin renombrar:
`LiveSession`, `SessionTeam`, `EvidenceSubmission`, `ValidationOutcome`, el override del operador y el estado de progreso por equipo. El **Session Flow** es la lista ordenada de **Plays** que el equipo recorre.

### Por que "play" y no "stage"

- "stage" suena a fase/etapa grande; una pregunta Kahoot es una **jugada** (un movimiento), no una etapa.
- "stage" ya quedo retirado en autoria (era la hoja del arbol del profe); mantenerlo en runtime reimporta ese modelo mental muerto.
- el atomo tiene UNA identidad que cruza 3 servicios (mission lo crea, session lo corre, scoring lo puntua); un solo nombre (`PlayId`) elimina la capa de traduccion donde aparecen los bugs de wiring.

## 6. Web (creacion de mision) — plan

Stack: React 19 + Vite 8 + react-router 7, sin lib de estado, `fetch` crudo, CSS propio.

**Reescribir** `src/apps/web/src/components/missions-admin-workspace.tsx` (~1800 lineas, editor de arbol recursivo): quitar `MissionNodeEditor` recursivo, `updateNodeInTree`/`removeNodeFromTree`/`makeComposite`/`makeLeaf` y la dualidad leaf/composite.

Nuevos building blocks:
- **Lista lineal de items** con drag-reorder + outline colapsable de Sections. Tipo `MissionItem = Section | Challenge`.
- **SectionEditor**: `{ Title, Order, children }` (inerte; sin datos de juego).
- **ChallengeEditor**: shared `{ Title, GameType, Difficulty, TimeLimit, isActive }` + sub-editor por tipo.
- **TriviaChallengeEditor** (nuevo, `trivia-challenge-editor.tsx`): lista de Questions; cada una texto + 2..4 Choices con toggle de correcta (radio).
- **TreasureHuntChallengeEditor**: Clue + ExpectedQrHash + Hints.
- **Reusar** `hint-editor.tsx` tal cual.
- Reemplazar `game-type-config.tsx` (hoy texto libre de trivia) por los editores de arriba.
- Drag-reorder: agregar `@dnd-kit/*` (compatible React 19).

Tipos cliente + API: reemplazar el arbol `MissionNode` por payload nuevo (Items / Section / Challenge / Question / Choice / Search). Endpoints CRUD de `MissionsController` sin cambio de ruta, solo shape.

Operator (`LiveSessionsWorkspace`, `OperatorPage`): consume plays planas; cambios menores de campos (la play trae choices), no estructura.

## 7. Mobile (partida) — plan

Stack: React Native + Expo 54 + expo-router + expo-camera + SignalR. App productiva (no scaffold). **Solo cambia el camino de Trivia; QR intacto.**

- `app/mobile/(participant)/board.tsx:525` — reemplazar `<TextInput>` por grilla de 2..4 botones de choice; estado `selectedChoiceId`; submit habilitado al elegir.
- `board.tsx:457,497` — wording: "Escribe la respuesta" -> "Selecciona la respuesta correcta".
- `src/lib/api-client.ts:106` — `SubmitTriviaAnswerInput.answerText -> selectedChoiceId`.
- `api-client.ts:128` `CurrentSessionStageSnapshot` — agregar `choices: { id, text }[]` (SIN correcta).
- `api-client.ts:282` `submitTriviaAnswer` — manda `selectedChoiceId`.
- QR (`board.tsx:312`, `:634`) — **sin cambios**.
- Progresion + SignalR (`ReceiveTeamProgressChanged`) — **sin cambios**.

## 8. Orden de implementacion (vertical slices, contract-first)

1. **mission-management**: domain nuevo + schema relacional + flatten + `EligiblePlaySnapshot` (con choices). Borrar `MissionNode`/`MissionStage` legacy + `NodeTreeJson`.
2. **scoring-monitoring**: rename `stage -> play` (DTO/DB/vocab). Logica intacta.
3. **session-management**: consumir `EligiblePlaySnapshot`; play fields + choices + `CorrectChoiceId`; Trivia texto->choiceId; `SubmittedChoiceId`; ocultar correcta al jugador; rename vocab.
4. **web**: editor lineal + Kahoot question editor + tipos cliente.
5. **mobile**: UI de choices + payload + wording.
6. **docs**: `CONTEXT.md` de session + scoring; relacion en `CONTEXT-MAP.md`.

Cada slice compila y testea solo. 1->3 son contract-first (definir `EligiblePlaySnapshot` primero desbloquea session, web y mobile en paralelo).

## 9. Reglas criticas

- **No filtrar la respuesta**: `CorrectChoiceId`/`IsCorrect` jamas al cliente jugador. El snapshot del jugador lleva `Choices { id, text }`; la validacion es server-side. (En autoria/web el admin si ve la correcta; en reveal post-partida tambien.)
- **DB descartable**: recrear schemas + migraciones libremente, sin migracion de datos.
- **Rutas estables**: mantener paths HTTP (`/trivia-submissions`, `/scores`, CRUD de missions); cambia el payload, no la URL -> menos churn en clientes.
- **SignalR intacto**: el realtime de progreso/scoreboard no cambia.
