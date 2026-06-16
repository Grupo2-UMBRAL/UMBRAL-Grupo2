const { execSync } = require("child_process");

const EDGE_PROXY_URL = "http://localhost:7500/session-operations";
const SESSION_ID = "032b3c2e-e593-4589-92f3-73338499f16f";
const TEAM_ID = "84b6f342-ff80-444a-9628-4d968e77fb18";

// Helper to get fresh tokens
function getTokens() {
  console.log("🔑 Fetching Keycloak Access Tokens...");

  const participantRes = execSync(
    `curl -s -X POST http://localhost:7500/auth/realms/umbral/protocol/openid-connect/token ` +
      `-d "client_id=umbral-web" -d "grant_type=password" -d "username=participant" -d "password=participant123!" -d "scope=openid"`,
  ).toString();
  const participantToken = JSON.parse(participantRes).access_token;

  const operatorRes = execSync(
    `curl -s -X POST http://localhost:7500/auth/realms/umbral/protocol/openid-connect/token ` +
      `-d "client_id=umbral-web" -d "grant_type=password" -d "username=operator" -d "password=operator123!" -d "scope=openid"`,
  ).toString();
  const operatorToken = JSON.parse(operatorRes).access_token;

  return { participantToken, operatorToken };
}

async function run() {
  const { participantToken, operatorToken } = getTokens();

  console.log("✅ Tokens obtained successfully.");

  // 1. Start the LiveSession
  console.log("\n🎬 Step 1: Starting LiveSession from Operator role...");
  const startRes = await fetch(
    `${EDGE_PROXY_URL}/api/session-operations/live-sessions/${SESSION_ID}/lifecycle/start`,
    {
      method: "POST",
      headers: {
        Authorization: `Bearer ${operatorToken}`,
        "Content-Type": "application/json",
      },
    },
  );
  console.log(
    `📡 Start Session Status: ${startRes.status} ${startRes.statusText}`,
  );
  if (!startRes.ok) {
    console.error("❌ Failed to start session:", await startRes.text());
    process.exit(1);
  }
  console.log("✅ LiveSession started successfully.");

  // 2. Fetch Participant Snapshot
  console.log("\n📋 Step 2: Fetching Participant Snapshot...");
  const snapshotRes1 = await fetch(
    `${EDGE_PROXY_URL}/api/session-operations/session-teams/${TEAM_ID}/snapshot`,
    {
      headers: { Authorization: `Bearer ${participantToken}` },
    },
  );
  const snapshot1 = await snapshotRes1.json();
  console.log(`📍 Current Stage: ${snapshot1.currentStage?.name}`);
  console.log(`❓ Prompt: "${snapshot1.currentStage?.prompt}"`);
  console.log(`📈 Progression State: ${snapshot1.progressState}`);

  // 3. Submit wrong answer for Stage 1
  console.log('\n❌ Step 3: Submitting WRONG answer ("Londres")...');
  const wrongRes = await fetch(
    `${EDGE_PROXY_URL}/api/session-operations/session-teams/${TEAM_ID}/trivia-submissions`,
    {
      method: "POST",
      headers: {
        Authorization: `Bearer ${participantToken}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ answerText: "Londres" }),
    },
  );
  const wrongResult = await wrongRes.json();
  console.log(`📡 Submission Outcome: ${wrongResult.validationOutcome}`);
  console.log(`📉 Progression State: ${wrongResult.progressState}`);

  // 4. Submit correct answer for Stage 1
  console.log('\n✅ Step 4: Submitting CORRECT answer ("París")...');
  const correctRes1 = await fetch(
    `${EDGE_PROXY_URL}/api/session-operations/session-teams/${TEAM_ID}/trivia-submissions`,
    {
      method: "POST",
      headers: {
        Authorization: `Bearer ${participantToken}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ answerText: "París" }),
    },
  );
  const correctResult1 = await correctRes1.json();
  console.log(`📡 Submission Outcome: ${correctResult1.validationOutcome}`);
  console.log(`📈 Progression State: ${correctResult1.progressState}`);

  // 5. Fetch Participant Snapshot for Stage 2
  console.log("\n📋 Step 5: Fetching Participant Snapshot again...");
  const snapshotRes2 = await fetch(
    `${EDGE_PROXY_URL}/api/session-operations/session-teams/${TEAM_ID}/snapshot`,
    {
      headers: { Authorization: `Bearer ${participantToken}` },
    },
  );
  const snapshot2 = await snapshotRes2.json();
  console.log(`📍 Current Stage: ${snapshot2.currentStage?.name}`);
  console.log(`❓ Prompt: "${snapshot2.currentStage?.prompt}"`);
  console.log(`📈 Progression State: ${snapshot2.progressState}`);

  // 6. Submit correct answer for Stage 2
  console.log(
    '\n✅ Step 6: Submitting CORRECT answer ("Mercurio") for Stage 2...',
  );
  const correctRes2 = await fetch(
    `${EDGE_PROXY_URL}/api/session-operations/session-teams/${TEAM_ID}/trivia-submissions`,
    {
      method: "POST",
      headers: {
        Authorization: `Bearer ${participantToken}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ answerText: "Mercurio" }),
    },
  );
  const correctResult2 = await correctRes2.json();
  console.log(`📡 Submission Outcome: ${correctResult2.validationOutcome}`);
  console.log(`📈 Progression State: ${correctResult2.progressState}`);

  // 7. Finalize LiveSession from Operator role
  console.log("\n🏁 Step 7: Finalizing LiveSession from Operator role...");
  const finalizeRes = await fetch(
    `${EDGE_PROXY_URL}/api/session-operations/live-sessions/${SESSION_ID}/lifecycle/finalize`,
    {
      method: "POST",
      headers: {
        Authorization: `Bearer ${operatorToken}`,
        "Content-Type": "application/json",
      },
    },
  );
  console.log(
    `📡 Finalize Session Status: ${finalizeRes.status} ${finalizeRes.statusText}`,
  );
  if (!finalizeRes.ok) {
    console.error("❌ Failed to finalize session:", await startRes.text());
    process.exit(1);
  }
  console.log("✅ LiveSession finalized successfully.");

  // 8. Fetch Final Participant Snapshot
  console.log("\n📋 Step 8: Fetching Final Participant Snapshot...");
  const snapshotRes3 = await fetch(
    `${EDGE_PROXY_URL}/api/session-operations/session-teams/${TEAM_ID}/snapshot`,
    {
      headers: { Authorization: `Bearer ${participantToken}` },
    },
  );
  const snapshot3 = await snapshotRes3.json();
  console.log(`📈 Final Progression State: ${snapshot3.progressState}`);
  console.log(`🎮 Session State: ${snapshot3.sessionState}`);
  console.log("\n🎉 GAME LOOP VERIFICATION SUCCESSFULLY COMPLETED!");
}

run().catch((err) => {
  console.error("💥 Error in execution:", err);
  process.exit(1);
});
