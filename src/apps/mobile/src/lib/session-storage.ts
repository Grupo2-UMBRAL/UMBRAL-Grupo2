import AsyncStorage from "@react-native-async-storage/async-storage";
import type { UmbralRole } from "./roles";

const sessionStorageKey = "umbral.mobile.session";

export type UmbralMobileSession = {
  accessToken: string;
  expiresAt: string;
  roles: UmbralRole[];
  username: string;
  displayName: string;
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

export function isSessionExpired(session: UmbralMobileSession) {
  return Date.parse(session.expiresAt) <= Date.now();
}
