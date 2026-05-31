export const umbralRoles = ["Administrator", "Operator", "Participant"] as const;
export const participantShellRoles = ["Participant"] as const;

export type UmbralRole = (typeof umbralRoles)[number];
export type ParticipantShellRole = (typeof participantShellRoles)[number];

export function isUmbralRole(value: string): value is UmbralRole {
  return umbralRoles.includes(value as UmbralRole);
}

export function uniqueRoles(roles: string[]): UmbralRole[] {
  return Array.from(new Set(roles.filter(isUmbralRole)));
}
