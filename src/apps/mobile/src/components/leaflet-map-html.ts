export type LatLng = {
  latitude: number;
  longitude: number;
};

/**
 * Builds a self-contained Leaflet + OpenStreetMap HTML document that plots the
 * hint location and, when known, the participant position. Shared by the native
 * (react-native-webview) and web (iframe) renderers so both platforms show the
 * exact same interactive map without any Google Maps API key or dev build.
 */
export function buildLeafletHtml(hint: LatLng, user: LatLng | null) {
  const userMarker = user
    ? `L.circleMarker([${user.latitude}, ${user.longitude}], { radius: 8, color: '#ffffff', weight: 2, fillColor: '#1CB0F6', fillOpacity: 1 }).addTo(map).bindPopup('Tú');`
    : "";

  const framing = user
    ? `map.fitBounds([[${hint.latitude}, ${hint.longitude}], [${user.latitude}, ${user.longitude}]], { padding: [48, 48], maxZoom: 16 });`
    : `map.setView([${hint.latitude}, ${hint.longitude}], 15);`;

  return `<!DOCTYPE html>
<html>
  <head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no" />
    <link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css" />
    <style>
      html, body, #map { height: 100%; margin: 0; padding: 0; background: #DCEEFB; }
      .leaflet-container { font-family: inherit; }
    </style>
  </head>
  <body>
    <div id="map"></div>
    <script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>
    <script>
      var map = L.map('map', { zoomControl: true, attributionControl: false });
      L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 19 }).addTo(map);
      L.circleMarker([${hint.latitude}, ${hint.longitude}], { radius: 9, color: '#ffffff', weight: 2, fillColor: '#EA2B2B', fillOpacity: 1 }).addTo(map).bindPopup('Pista');
      ${userMarker}
      ${framing}
    </script>
  </body>
</html>`;
}
