import { readFile, writeFile } from "node:fs/promises";
import { networkInterfaces } from "node:os";
import { resolve } from "node:path";

// Genera .env.local a partir de .env.development pero reemplazando `localhost`
// por la IP LAN de esta maquina, para que Expo Go en el telefono alcance el
// stack de docker compose corriendo en tu PC (edge-proxy en :7500).
//
// El telefono suele conectarse por Wi-Fi, asi que preferimos ese adaptador y
// descartamos VPN / WSL / adaptadores virtuales. Si la deteccion falla, forza
// la IP con la variable de entorno: LAN_IP=192.168.x.x npm run start:lan

// Nombres de adaptadores que NO son la LAN real del telefono.
const VIRTUAL_ADAPTER = /vethernet|wsl|virtualbox|vbox|hyper-?v|tailscale|wireguard|vpn|cyberghost|loopback|docker|zerotier|hamachi|npcap/i;
const WIFI_ADAPTER = /wi-?fi|wlan|wireless|inalambr/i;

function collectCandidates() {
  const candidates = [];
  for (const [name, addresses] of Object.entries(networkInterfaces())) {
    for (const address of addresses ?? []) {
      if (address.family !== "IPv4" || address.internal) continue;
      if (address.address.startsWith("169.254.")) continue; // link-local, sin red
      if (address.netmask === "255.255.255.255") continue; // punto-a-punto (VPN)
      if (VIRTUAL_ADAPTER.test(name)) continue;
      candidates.push({ name, ip: address.address });
    }
  }
  return candidates;
}

function rank({ name, ip }) {
  let score = 0;
  if (!WIFI_ADAPTER.test(name)) score += 10; // preferir Wi-Fi
  if (ip.startsWith("192.168.")) score += 0;
  else if (ip.startsWith("10.")) score += 1;
  else if (/^172\.(1[6-9]|2\d|3[0-1])\./.test(ip)) score += 2;
  else score += 5;
  if (ip.endsWith(".0") || ip.endsWith(".1")) score += 20; // red/gateway, no host
  return score;
}

const override = process.env.LAN_IP;
const candidates = collectCandidates().sort((a, b) => rank(a) - rank(b));
const lanIp = override ?? candidates[0]?.ip;

if (!lanIp) {
  throw new Error(
    "No se detecto ninguna IP LAN. Conectate a WiFi/Ethernet o forza una con LAN_IP=<tu-ip>."
  );
}

const source = resolve(".env.development");
const destination = resolve(".env.local");

const base = await readFile(source, "utf8");
const patched = base.replaceAll("localhost", lanIp);

await writeFile(destination, patched);

console.log(`Selected LAN environment: localhost -> ${lanIp} (escrito en .env.local).`);
if (!override && candidates.length > 1) {
  const others = candidates.slice(1).map((c) => `${c.ip} (${c.name})`).join(", ");
  console.log(`Si no es la correcta, forza con LAN_IP=<ip>. Otras detectadas: ${others}.`);
}
console.log("El telefono y la PC deben estar en la misma red WiFi.");
