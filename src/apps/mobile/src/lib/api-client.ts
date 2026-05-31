import { getClientConfig } from "./config";

function normalizePath(path: string) {
  return path.startsWith("/") ? path : `/${path}`;
}

function createAuthorizationHeaderPreview(accessToken: string) {
  return `Bearer ${accessToken.slice(0, 18)}...`;
}

export type ApiProbeResult = {
  ok: boolean;
  status: number;
  body: string;
  url: string;
  authorizationHeaderPreview: string;
};

export function createAuthorizedApiClient(accessToken: string) {
  const config = getClientConfig();

  async function request(path: string, init: RequestInit = {}) {
    const url = `${config.edgeProxyPublicBaseUrl}${normalizePath(path)}`;
    const headers = new Headers(init.headers);
    headers.set("authorization", `Bearer ${accessToken}`);

    if (init.body && !headers.has("content-type")) {
      headers.set("content-type", "application/json");
    }

    const response = await fetch(url, {
      ...init,
      headers
    });

    return {
      url,
      response
    };
  }

  return {
    request,
    authorizationHeaderPreview: createAuthorizationHeaderPreview(accessToken),
    async getHealth(): Promise<ApiProbeResult> {
      const { url, response } = await request("/health");

      return {
        ok: response.ok,
        status: response.status,
        body: await response.text(),
        url,
        authorizationHeaderPreview: createAuthorizationHeaderPreview(accessToken)
      };
    }
  };
}
