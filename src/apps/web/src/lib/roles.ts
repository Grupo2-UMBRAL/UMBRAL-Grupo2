export const umbralRoles = ["Administrator", "Operator", "Participant"] as const;
export const webShellRoles = ["Administrator", "Operator"] as const;

export type UmbralRole = (typeof umbralRoles)[number];
export type WebShellRole = (typeof webShellRoles)[number];

export function isUmbralRole(value: string): value is UmbralRole {
  return umbralRoles.includes(value as UmbralRole);
}

export function isWebShellRole(value: string): value is WebShellRole {
  return webShellRoles.includes(value as WebShellRole);
}

export function uniqueRoles(roles: string[]): UmbralRole[] {
  return Array.from(new Set(roles.filter(isUmbralRole)));
}

export function roleLabel(role: WebShellRole) {
  return role === "Administrator" ? "Espacio de trabajo del Administrador" : "Espacio de trabajo del Operador";
}
