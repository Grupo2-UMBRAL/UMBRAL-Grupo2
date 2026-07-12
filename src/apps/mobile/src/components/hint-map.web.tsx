import { useEffect, useState } from "react";
import { Linking, StyleSheet, Text, View } from "react-native";
import { GameButton } from "./game-button";
import { buildLeafletHtml, type LatLng } from "./leaflet-map-html";
import { buildGoogleMapsDirectionsUrl } from "./maps-links";
import { colors } from "../theme/tokens";

type HintMapProps = {
  latitude: number;
  longitude: number;
};

function formatCoordinate(value: number) {
  return value.toFixed(5);
}

/**
 * Web renderer for the hint map. react-native-webview has no react-native-web
 * build, so on web we drop the same Leaflet document into a real DOM iframe and
 * read the participant position from the browser Geolocation API. Keeps full
 * feature parity with the native WebView renderer in `hint-map.tsx`.
 */
export function HintMap({ latitude, longitude }: HintMapProps) {
  const [userLocation, setUserLocation] = useState<LatLng | null>(null);
  const [locationBlocked, setLocationBlocked] = useState(false);

  useEffect(() => {
    if (typeof navigator === "undefined" || !navigator.geolocation) {
      setLocationBlocked(true);
      return;
    }

    let active = true;
    navigator.geolocation.getCurrentPosition(
      (position) => {
        if (!active) {
          return;
        }
        setUserLocation({
          latitude: position.coords.latitude,
          longitude: position.coords.longitude
        });
      },
      () => {
        if (active) {
          setLocationBlocked(true);
        }
      }
    );

    return () => {
      active = false;
    };
  }, []);

  const html = buildLeafletHtml({ latitude, longitude }, userLocation);

  const openDirections = () => {
    void Linking.openURL(buildGoogleMapsDirectionsUrl(latitude, longitude));
  };

  return (
    <View style={styles.container}>
      <Text style={styles.title}>Mapa de la pista</Text>
      <View style={styles.mapFrame}>
        <iframe
          title="Mapa de la pista"
          srcDoc={html}
          style={{ border: "0", width: "100%", height: "100%" }}
        />
      </View>
      <Text style={styles.coordinates}>
        Pista: Lat {formatCoordinate(latitude)} | Lon {formatCoordinate(longitude)}
        {locationBlocked ? " · Activa la ubicación para verte en el mapa" : ""}
      </Text>
      <GameButton label="Cómo llegar" icon="🧭" variant="ghost" onPress={openDirections} />
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    backgroundColor: colors.surface.raised,
    borderColor: colors.surface.raisedBorder,
    borderRadius: 18,
    borderWidth: 1,
    gap: 10,
    padding: 14
  },
  title: {
    color: colors.text.primary,
    fontSize: 14,
    fontWeight: "700"
  },
  mapFrame: {
    backgroundColor: colors.surface.mapFrame,
    borderRadius: 16,
    height: 180,
    overflow: "hidden",
    position: "relative"
  },
  coordinates: {
    color: colors.text.tertiary,
    fontSize: 13,
    fontWeight: "600"
  }
});
