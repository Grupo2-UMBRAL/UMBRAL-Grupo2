/**
 * Deep link that opens the participant's native Google Maps app (or the web
 * fallback) with turn-by-turn directions from their current location to the
 * hint. Uses the platform-agnostic Google Maps URL scheme, so it works from the
 * WebView map, Expo Go, and the web build without any SDK.
 */
export function buildGoogleMapsDirectionsUrl(latitude: number, longitude: number) {
  return `https://www.google.com/maps/dir/?api=1&destination=${latitude},${longitude}`;
}
