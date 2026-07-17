import AsyncStorage from "@react-native-async-storage/async-storage";
import type { UmbralRole } from "./roles";

const sessionStorageKey = "umbral.mobile.session";
const enrollmentStorageKey = "umbral.mobile.enrollment";
const onboardingStorageKey = "umbral.mobile.onboarding";

export type UmbralMobileSession = {
  accessToken: string;
  expiresAt: string;
  // Kept so the session can be renewed while the player is still in a live game. Older stored
  // sessions (pre-refresh support) may not carry these; treat them as non-renewable.
  refreshToken?: string;
  refreshExpiresAt?: string;
  roles: UmbralRole[];
  username: string;
  displayName: string;
};

export type StoredEnrollment = {
  joinCode: string;
  teamId: string;
  teamName: string;
};

export async function loadStoredSession() {
  const rawValue = await AsyncStorage.getItem(sessionStorageKey);
  if (!rawValue) {
    return null;
  }

  try {
    return JSON.parse(rawValue) as UmbralMobileSession;
  } catch {
    await AsyncStorage.removeItem(sessionStorageKey);
    return null;
  }
}

export function saveStoredSession(session: UmbralMobileSession) {
  return AsyncStorage.setItem(sessionStorageKey, JSON.stringify(session));
}

export function clearStoredSession() {
  return AsyncStorage.removeItem(sessionStorageKey);
}

export async function loadStoredEnrollment() {
  const rawValue = await AsyncStorage.getItem(enrollmentStorageKey);
  if (!rawValue) {
    return null;
  }

  try {
    return JSON.parse(rawValue) as StoredEnrollment;
  } catch {
    await AsyncStorage.removeItem(enrollmentStorageKey);
    return null;
  }
}

export function saveStoredEnrollment(enrollment: StoredEnrollment) {
  return AsyncStorage.setItem(enrollmentStorageKey, JSON.stringify(enrollment));
}

export function clearStoredEnrollment() {
  return AsyncStorage.removeItem(enrollmentStorageKey);
}

export function isSessionExpired(session: UmbralMobileSession) {
  return Date.parse(session.expiresAt) <= Date.now();
}

/**
 * Qué piezas de la entrada ya vio el participante durante el inicio de sesión actual. Se reinicia
 * al autenticar de nuevo para que el tutorial se muestre en cada login; no se toca durante las
 * renovaciones de token ni al cambiar de sesión de juego.
 */
export type OnboardingState = {
  tutorialSeen: boolean;
  checklistDismissed: boolean;
};

const defaultOnboarding: OnboardingState = { tutorialSeen: false, checklistDismissed: false };

export async function loadOnboardingState(): Promise<OnboardingState> {
  const rawValue = await AsyncStorage.getItem(onboardingStorageKey);
  if (!rawValue) {
    return defaultOnboarding;
  }

  try {
    return { ...defaultOnboarding, ...(JSON.parse(rawValue) as Partial<OnboardingState>) };
  } catch {
    await AsyncStorage.removeItem(onboardingStorageKey);
    return defaultOnboarding;
  }
}

export async function saveOnboardingState(patch: Partial<OnboardingState>) {
  const current = await loadOnboardingState();
  return AsyncStorage.setItem(onboardingStorageKey, JSON.stringify({ ...current, ...patch }));
}

export function resetOnboardingState() {
  return AsyncStorage.setItem(onboardingStorageKey, JSON.stringify(defaultOnboarding));
}
