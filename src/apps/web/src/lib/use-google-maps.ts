import { useEffect, useState } from "react";
import { importLibrary, setOptions } from "@googlemaps/js-api-loader";

export type GoogleMapsStatus = "disabled" | "loading" | "ready" | "error";

// A single load is shared across every hint picker mounted on the page: the
// Maps JS SDK is a page-global singleton and must only be injected once. Keyed
// by api key so a (theoretical) key change re-loads rather than serving stale.
let sharedLoad: { key: string; promise: Promise<void> } | null = null;

function loadGoogleMaps(apiKey: string): Promise<void> {
  if (sharedLoad && sharedLoad.key === apiKey) {
    return sharedLoad.promise;
  }

  // setOptions must run before the first importLibrary call (functional API;
  // the Loader class is deprecated in @googlemaps/js-api-loader v2).
  setOptions({ key: apiKey, v: "weekly" });
  const promise = Promise.all([
    importLibrary("maps"),
    importLibrary("places"),
  ]).then(() => undefined);

  sharedLoad = { key: apiKey, promise };
  return promise;
}

/**
 * Loads the Google Maps JS SDK (maps + places libraries) once and reports its
 * status. With no api key the hook stays "disabled" and never touches the
 * network, so authoring keeps working offline / in tests via the manual
 * lat/lng inputs.
 */
export function useGoogleMaps(apiKey: string): GoogleMapsStatus {
  // Initial value covers the synchronous cases ("disabled" with no key,
  // "loading" while the SDK loads); setStatus is only called asynchronously
  // from the load promise below. Config is static per app load, so apiKey does
  // not change across renders in practice.
  const [status, setStatus] = useState<GoogleMapsStatus>(
    apiKey ? "loading" : "disabled",
  );

  useEffect(() => {
    if (!apiKey) {
      return;
    }

    let active = true;

    loadGoogleMaps(apiKey)
      .then(() => {
        if (active) {
          setStatus("ready");
        }
      })
      .catch(() => {
        if (active) {
          setStatus("error");
        }
      });

    return () => {
      active = false;
    };
  }, [apiKey]);

  return status;
}
