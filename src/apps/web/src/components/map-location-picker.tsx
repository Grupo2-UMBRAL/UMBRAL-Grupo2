import { useEffect, useRef } from "react";
import { getClientConfig } from "../lib/config";
import { useGoogleMaps } from "../lib/use-google-maps";

type MapLocationPickerProps = {
  latitude: string;
  longitude: string;
  onChange: (latitude: string, longitude: string) => void;
};

// Fallback view when the author has not set coordinates yet. Bogotá, matching
// the country the example coordinates in the manual inputs point at.
const DEFAULT_CENTER = { lat: 4.60971, lng: -74.08175 };
const DEFAULT_ZOOM = 11;
const PLACED_ZOOM = 16;

function parseLatLng(
  latitude: string,
  longitude: string,
): google.maps.LatLngLiteral | null {
  const lat = Number.parseFloat(latitude);
  const lng = Number.parseFloat(longitude);
  if (!Number.isFinite(lat) || !Number.isFinite(lng)) {
    return null;
  }
  return { lat, lng };
}

/**
 * Map + Places search box that writes the picked point back through `onChange`
 * as strings, matching the shape the manual lat/lng inputs already use. Renders
 * nothing when there is no Google Maps key so the manual inputs stay the sole
 * (working) UI.
 */
export function MapLocationPicker({
  latitude,
  longitude,
  onChange,
}: MapLocationPickerProps) {
  const { googleMapsApiKey } = getClientConfig();
  const status = useGoogleMaps(googleMapsApiKey);

  const mapNodeRef = useRef<HTMLDivElement | null>(null);
  const searchNodeRef = useRef<HTMLInputElement | null>(null);
  const mapRef = useRef<google.maps.Map | null>(null);
  const markerRef = useRef<google.maps.Marker | null>(null);

  // Keep the latest onChange without re-running the one-shot init effect.
  const onChangeRef = useRef(onChange);
  useEffect(() => {
    onChangeRef.current = onChange;
  }, [onChange]);

  useEffect(() => {
    if (status !== "ready" || !mapNodeRef.current) {
      return;
    }

    const initial = parseLatLng(latitude, longitude);
    const map = new google.maps.Map(mapNodeRef.current, {
      center: initial ?? DEFAULT_CENTER,
      zoom: initial ? PLACED_ZOOM : DEFAULT_ZOOM,
      streetViewControl: false,
      mapTypeControl: false,
      fullscreenControl: false,
    });
    const marker = new google.maps.Marker({
      map,
      position: initial ?? undefined,
      draggable: true,
    });
    mapRef.current = map;
    markerRef.current = marker;

    function commit(position: google.maps.LatLng | null | undefined) {
      if (!position) {
        return;
      }
      onChangeRef.current(position.lat().toString(), position.lng().toString());
    }

    marker.addListener("dragend", () => commit(marker.getPosition()));
    map.addListener("click", (event: google.maps.MapMouseEvent) => {
      if (!event.latLng) {
        return;
      }
      marker.setPosition(event.latLng);
      commit(event.latLng);
    });

    if (searchNodeRef.current) {
      const autocomplete = new google.maps.places.Autocomplete(
        searchNodeRef.current,
        { fields: ["geometry"] },
      );
      autocomplete.bindTo("bounds", map);
      autocomplete.addListener("place_changed", () => {
        const location = autocomplete.getPlace().geometry?.location;
        if (!location) {
          return;
        }
        map.setCenter(location);
        map.setZoom(PLACED_ZOOM);
        marker.setPosition(location);
        commit(location);
      });
    }

    return () => {
      google.maps.event.clearInstanceListeners(marker);
      google.maps.event.clearInstanceListeners(map);
      mapRef.current = null;
      markerRef.current = null;
    };
    // Init runs once the SDK is ready; live lat/lng sync is handled below so the
    // map is not torn down and rebuilt on every keystroke in the manual inputs.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [status]);

  // Reflect external coordinate changes (manual inputs) onto the marker/map.
  useEffect(() => {
    const map = mapRef.current;
    const marker = markerRef.current;
    if (status !== "ready" || !map || !marker) {
      return;
    }

    const next = parseLatLng(latitude, longitude);
    if (!next) {
      marker.setPosition(null);
      return;
    }

    const current = marker.getPosition();
    if (current && current.lat() === next.lat && current.lng() === next.lng) {
      return;
    }
    marker.setPosition(next);
    map.setCenter(next);
  }, [latitude, longitude, status]);

  if (status === "disabled") {
    return null;
  }

  return (
    <div className="map-picker">
      <input
        className="form-input"
        disabled={status !== "ready"}
        placeholder="Buscar un lugar o dirección…"
        ref={searchNodeRef}
        type="text"
      />
      <div
        aria-label="Mapa para elegir la ubicación de la pista"
        className="map-picker-canvas"
        ref={mapNodeRef}
        role="application"
      />
      {status === "loading" ? (
        <p className="form-hint">Cargando el mapa…</p>
      ) : null}
      {status === "error" ? (
        <p className="form-hint">
          No se pudo cargar el mapa. Ingresa las coordenadas manualmente abajo.
        </p>
      ) : null}
    </div>
  );
}
