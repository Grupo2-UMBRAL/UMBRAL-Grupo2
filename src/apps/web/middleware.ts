import { NextRequest, NextResponse } from "next/server";
import {
  parseSessionCookieValue,
  resolvePathRoles,
  sessionCookieName,
  sessionHasAnyRole
} from "@/lib/session-cookie";

const authFreePrefixes = ["/login", "/forbidden", "/api/auth/login", "/api/auth/logout"];

function isAuthFreePath(pathname: string) {
  return authFreePrefixes.some((prefix) => pathname === prefix || pathname.startsWith(`${prefix}/`));
}

function isFrameworkPath(pathname: string) {
  return (
    pathname.startsWith("/_next") ||
    pathname.startsWith("/favicon") ||
    pathname.startsWith("/robots") ||
    pathname.startsWith("/sitemap")
  );
}

export function middleware(request: NextRequest) {
  const { pathname, search } = request.nextUrl;

  if (pathname === "/coverage") {
    const edgeProxyUrl = process.env.NEXT_PUBLIC_EDGE_PROXY_BASE_URL || "http://localhost:7500";
    return NextResponse.redirect(new URL("/coverage/", edgeProxyUrl).toString());
  }

  if (isFrameworkPath(pathname) || isAuthFreePath(pathname)) {
    return NextResponse.next();
  }

  const sessionCookie = request.cookies.get(sessionCookieName)?.value;
  const session = parseSessionCookieValue(sessionCookie);

  if (!session) {
    const loginUrl = new URL("/login", request.url);
    const nextPath = pathname === "/" ? "/dashboard" : `${pathname}${search}`;
    loginUrl.searchParams.set("next", nextPath);
    return NextResponse.redirect(loginUrl);
  }

  const requiredRoles = resolvePathRoles(pathname);
  if (requiredRoles.length > 0 && !sessionHasAnyRole(session, requiredRoles)) {
    return NextResponse.redirect(new URL("/forbidden", request.url));
  }

  if (pathname === "/") {
    return NextResponse.redirect(new URL("/dashboard", request.url));
  }

  if (pathname === "/dashboard") {
    const dashboardPath = session.roles.includes("Administrator")
      ? "/administrator"
      : session.roles.includes("Operator")
        ? "/operator"
        : "/forbidden";
    return NextResponse.redirect(new URL(dashboardPath, request.url));
  }

  return NextResponse.next();
}

export const config = {
  matcher: ["/((?!_next/static|_next/image|favicon.ico).*)"]
};
