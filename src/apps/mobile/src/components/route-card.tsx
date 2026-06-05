import { Pressable, StyleSheet, Text, View } from "react-native";

type RouteCardProps = {
  title: string;
  description: string;
  onPress: () => void;
};

export function RouteCard({ title, description, onPress }: RouteCardProps) {
  return (
    <Pressable onPress={onPress} style={({ pressed }) => [styles.card, pressed && styles.pressed]}>
      <View style={styles.copy}>
        <Text style={styles.title}>{title}</Text>
        <Text style={styles.description}>{description}</Text>
      </View>
      <Text style={styles.action}>Open</Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  card: {
    backgroundColor: "#fffaf5",
    borderRadius: 22,
    borderWidth: 1,
    borderColor: "#eadcc8",
    padding: 18,
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    gap: 16
  },
  pressed: {
    opacity: 0.8
  },
  copy: {
    flex: 1,
    gap: 6
  },
  title: {
    color: "#17313b",
    fontSize: 17,
    fontWeight: "700"
  },
  description: {
    color: "#4d5e64",
    fontSize: 14,
    lineHeight: 20
  },
  action: {
    color: "#1e6f8c",
    fontSize: 13,
    fontWeight: "700",
    textTransform: "uppercase"
  }
});
