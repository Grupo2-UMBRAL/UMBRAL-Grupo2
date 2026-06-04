import { StyleSheet, Text, View } from "react-native";

type StaticHintMapProps = {
  latitude: number;
  longitude: number;
};

function normalizeCoordinate(value: number) {
  const fractional = Math.abs(value % 1);
  return 0.2 + fractional * 0.6;
}

function formatCoordinate(value: number) {
  return value.toFixed(5);
}

export function StaticHintMap({ latitude, longitude }: StaticHintMapProps) {
  const pinTop = `${(1 - normalizeCoordinate(latitude)) * 100}%` as const;
  const pinLeft = `${normalizeCoordinate(longitude) * 100}%` as const;

  return (
    <View style={styles.container}>
      <Text style={styles.title}>Static map</Text>
      <View style={styles.mapFrame}>
        <View style={styles.gridHorizontal} />
        <View style={styles.gridVertical} />
        <View style={[styles.pin, { top: pinTop, left: pinLeft }]} />
        <View style={styles.compassRow}>
          <Text style={styles.compassLabel}>N</Text>
          <Text style={styles.compassLabel}>E</Text>
        </View>
      </View>
      <Text style={styles.coordinates}>
        Lat {formatCoordinate(latitude)} | Lon {formatCoordinate(longitude)}
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    backgroundColor: "#f2f6f7",
    borderColor: "#c8d7dc",
    borderRadius: 18,
    borderWidth: 1,
    gap: 10,
    padding: 14
  },
  title: {
    color: "#17313b",
    fontSize: 14,
    fontWeight: "700"
  },
  mapFrame: {
    backgroundColor: "#dcecef",
    borderRadius: 16,
    height: 140,
    overflow: "hidden",
    position: "relative"
  },
  gridHorizontal: {
    backgroundColor: "rgba(23, 49, 59, 0.12)",
    height: 1,
    left: 0,
    position: "absolute",
    right: 0,
    top: "50%"
  },
  gridVertical: {
    backgroundColor: "rgba(23, 49, 59, 0.12)",
    bottom: 0,
    position: "absolute",
    top: 0,
    width: 1,
    left: "50%"
  },
  pin: {
    backgroundColor: "#d94f30",
    borderColor: "#fffaf5",
    borderRadius: 10,
    borderWidth: 2,
    height: 16,
    marginLeft: -8,
    marginTop: -8,
    position: "absolute",
    width: 16
  },
  compassRow: {
    flexDirection: "row",
    justifyContent: "space-between",
    left: 12,
    position: "absolute",
    right: 12,
    top: 10
  },
  compassLabel: {
    color: "#40616d",
    fontSize: 12,
    fontWeight: "700"
  },
  coordinates: {
    color: "#40616d",
    fontSize: 13,
    fontWeight: "600"
  }
});
