import { copyFile, access } from "node:fs/promises";
import { constants } from "node:fs";
import { resolve } from "node:path";

const environment = process.argv[2];
const supportedEnvironments = new Set(["development", "deployment"]);

if (!supportedEnvironments.has(environment)) {
  throw new Error(
    `Expected one of: ${[...supportedEnvironments].join(", ")}. Received: ${environment ?? "nothing"}.`
  );
}

const source = resolve(`.env.${environment}`);
const destination = resolve(".env.local");

await access(source, constants.R_OK);
await copyFile(source, destination);

console.log(`Selected ${environment} environment from .env.${environment}.`);
