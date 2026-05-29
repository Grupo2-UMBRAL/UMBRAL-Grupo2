"use client";

import { startTransition, useState } from "react";
import { useRouter } from "next/navigation";

export function LogoutButton() {
  const router = useRouter();
  const [pending, setPending] = useState(false);

  async function handleLogout() {
    setPending(true);

    await fetch("/api/auth/logout", {
      method: "POST"
    });

    startTransition(() => {
      router.replace("/login");
      router.refresh();
    });
  }

  return (
    <button className="ghost-button" disabled={pending} onClick={() => void handleLogout()} type="button">
      {pending ? "Signing out..." : "Sign out"}
    </button>
  );
}
