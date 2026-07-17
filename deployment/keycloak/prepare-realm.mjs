import { readFile, writeFile } from 'node:fs/promises';

const publicBaseUrl = process.env.DEMO_PUBLIC_BASE_URL;
if (!publicBaseUrl?.startsWith('https://') || publicBaseUrl.endsWith('/')) {
  throw new Error('DEMO_PUBLIC_BASE_URL must be an HTTPS URL without a trailing slash.');
}

const realm = JSON.parse(
  await readFile('/build/umbral-realm.json', 'utf8'),
);

const webClient = realm.clients.find((client) => client.clientId === 'umbral-web');
if (!webClient) {
  throw new Error('The umbral-web client is missing from the realm import.');
}

webClient.redirectUris = [`${publicBaseUrl}/*`];
webClient.webOrigins = [publicBaseUrl];

await writeFile(
  '/build/umbral-realm.demo.json',
  `${JSON.stringify(realm, null, 2)}\n`,
);
