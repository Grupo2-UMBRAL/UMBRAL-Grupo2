"use client";

import { startTransition, useState, type FormEvent } from "react";
import { useRouter, useSearchParams } from "next/navigation";

type LoginState = {
  error: string | null;
  pending: boolean;
};

export function LoginForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const [state, setState] = useState<LoginState>({ error: null, pending: false });

  async function handleSubmit(formData: FormData) {
    if (state.pending) {
      return;
    }

    setState({ error: null, pending: true });

    try {
      const response = await fetch("/api/auth/login", {
        method: "POST",
        headers: {
          "content-type": "application/json"
        },
        body: JSON.stringify({
          username: formData.get("username"),
          password: formData.get("password"),
          next: searchParams.get("next")
        })
      });

      const payload = (await response.json().catch(() => null)) as
        | { redirectTo?: string; message?: string }
        | null;

      if (!response.ok) {
        await fetch("/api/auth/logout", {
          method: "POST"
        }).catch(() => null);

        setState({
          error: payload?.message ?? "Login failed. Verify credentials and role.",
          pending: false
        });
        return;
      }

      startTransition(() => {
        router.replace(payload?.redirectTo ?? "/dashboard");
        router.refresh();
      });
    } catch {
      setState({
        error: "Login request failed. Verify the local stack and try again.",
        pending: false
      });
    }
  }

  async function handleFormSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    await handleSubmit(new FormData(event.currentTarget));
  }

  return (
    <form className="auth-form" onSubmit={(event) => void handleFormSubmit(event)}>
      <label className="field">
        <span>Username</span>
        <input
          autoComplete="username"
          className="input"
          defaultValue="admin"
          name="username"
          placeholder="admin or operator"
          required
        />
      </label>

      <label className="field">
        <span>Password</span>
        <input
          autoComplete="current-password"
          className="input"
          defaultValue="admin123!"
          name="password"
          placeholder="Password"
          required
          type="password"
        />
      </label>

      {state.error ? <p className="form-error">{state.error}</p> : null}

      <button className="primary-button" disabled={state.pending} type="submit">
        {state.pending ? "Signing in..." : "Sign in to shell"}
      </button>
    </form>
  );
}
