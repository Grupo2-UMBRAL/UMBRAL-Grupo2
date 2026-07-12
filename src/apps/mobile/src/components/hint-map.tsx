import { useEffect, useState } from "react";
import { StyleSheet, Text, View } from "react-native";
import { WebView } from "react-native-webview";
import * as Location from "expo-location";
import { buildLeafletHtml, type LatLng } from "./leaflet-map-html";
import { colors } from "../theme/tokens";

type HintMapProps = {
  latitude: number;
  longitude: number;
};

function formatCoordinate(value: number) {
  return value.toFixed(5);
}

/**
 * Interactive Leaflet/OpenStreetMap view for a treasure-hunt hint, rendered in a
 * WebView. Shows the hint marker and, when the participant grants foreground
 * location permission, their own position — no Google Maps API key and no native
 * dev build required, so it works in Expo Go. The web build resolves
 * `hint-map.web.tsx`, which renders the same map inside an iframe.
 */
export function HintMap({ latitude, longitude }: HintMapProps) {
  const [userLocation, setUserLocation] = useState<LatLng | null>(null);
  const [locationBlocked, setLocationBlocked] = useState(false);

  useEffect(() => {
    let active = true;

    async function locateParticipant() {
      try {
        const permission = await Location.requestForegroundPermissionsAsync();
        if (!active) {
          return;
        }

        if (permission.status !== "granted") {
          setLocationBlocked(true);
          return;
        }

        const position = await Location.getCurrentPositionAsync({
          accuracy: Location.Accuracy.Balanced
        });
        if (!active) {
          return;
        }

        setUserLocation({
          latitude: position.coords.latitude,
          longitude: position.coords.longitude
        });
      } catch {
        if (active) {
          setLocationBlocked(true);
        }
      }
    }

    void locateParticipant();

    return () => {
      active = false;
    };
  }, []);

  const html = buildLeafletHtml({ latitude, longitude }, userLocation);

  return (
    <View style={styles.container}>
      <Text style={styles.title}>Mapa de la pista</Text>
      <View style={styles.mapFrame}>
        <WebView
          originWhitelist={["*"]}
          source={{ html }}
          style={styles.webview}
          scrollEnabled={false}
          javaScriptEnabled
          domStorageEnabled
        />
      </View>
      <Text style={styles.coordinates}>
        Pista: Lat {formatCoordinate(latitude)} | Lon {formatCoordinate(longitude)}
        {locationBlocked ? " · Activa la ubicación para verte en el mapa" : ""}
      </Text>
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
  webview: {
    backgroundColor: "transparent",
    flex: 1
  },
  coordinates: {
    color: colors.text.tertiary,
    fontSize: 13,
    fontWeight: "600"
  }
});
