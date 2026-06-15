import { useState } from "react";
import { useAuth } from "@/context/auth-context";

export function LogoutButton() {
  const { logout } = useAuth();
  const [pending, setPending] = useState(false);

  async function handleLogout() {
    setPending(true);
    try {
      await logout();
    } catch (error) {
      console.error("Logout failed:", error);
      setPending(false);
    }
  }

  return (
    <button className="btn btn-ghost btn-sm" disabled={pending} onClick={() => void handleLogout()} type="button">
      {pending ? "Cerrando sesión..." : "Cerrar sesión"}
    </button>
  );
}
