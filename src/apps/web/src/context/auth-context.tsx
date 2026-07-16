/* eslint-disable react-refresh/only-export-components */
import React, { createContext, useContext, useEffect, useState } from "react";
import Keycloak from "keycloak-js";
import { getClientConfig } from "@/lib/config";
import { extractUmbralRoles, readTokenIdentity } from "@/lib/jwt";
import type { UmbralRole } from "@/lib/roles";

type AuthContextType = {
  isAuthenticated: boolean;
  token: string | null;
  roles: UmbralRole[];
  user: { username: string; displayName: string } | null;
  loading: boolean;
  login: () => Promise<void>;
  logout: () => Promise<void>;
};

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [keycloak, setKeycloak] = useState<Keycloak | null>(null);
  const [isAuthenticated, setIsAuthenticated] = useState<boolean>(false);
  const [token, setToken] = useState<string | null>(null);
  const [roles, setRoles] = useState<UmbralRole[]>([]);
  const [user, setUser] = useState<{ username: string; displayName: string } | null>(null);
  const [loading, setLoading] = useState<boolean>(true);

  useEffect(() => {
    const config = getClientConfig();
    
    // Initialize Keycloak instance
    const kc = new Keycloak({
      url: config.keycloakPublicBaseUrl,
      realm: config.keycloakRealm,
      clientId: "umbral-web", // Matches KEYCLOAK_WEB_CLIENT_ID
    });

    kc.init({
      // Without check-sso the tokens, which live only in memory, are gone after a
      // page refresh and the user looks logged out despite a live Keycloak session.
      onLoad: "check-sso",
      pkceMethod: "S256",
      checkLoginIframe: false,
    })
      .then((authenticated) => {
        setKeycloak(kc);
        setIsAuthenticated(authenticated);
        if (authenticated && kc.token) {
          setToken(kc.token);
          setRoles(extractUmbralRoles(kc.token));
          setUser(readTokenIdentity(kc.token));
        }
        setLoading(false);
      })
      .catch((err) => {
        console.error("Keycloak initialization failed:", err);
        setLoading(false);
      });

    // Handle token refresh automatically
    const interval = setInterval(() => {
      if (kc.token) {
        kc.updateToken(70)
          .then((refreshed) => {
            if (refreshed && kc.token) {
              setToken(kc.token);
              setRoles(extractUmbralRoles(kc.token));
              setUser(readTokenIdentity(kc.token));
            }
          })
          .catch((err) => {
            console.error("Failed to refresh token", err);
          });
      }
    }, 60000);

    return () => clearInterval(interval);
  }, []);

  const login = async () => {
    if (keycloak) {
      await keycloak.login();
    }
  };

  const logout = async () => {
    if (keycloak) {
      await keycloak.logout({ redirectUri: window.location.origin });
    }
  };

  return (
    <AuthContext.Provider
      value={{
        isAuthenticated,
        token,
        roles,
        user,
        loading,
        login,
        logout,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used within an AuthProvider");
  }
  return context;
}
