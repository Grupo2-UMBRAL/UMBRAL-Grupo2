import { ActivityIndicator, StyleSheet, Text, View } from "react-native";
import { colors } from "../theme/tokens";

type LoadingScreenProps = {
  message?: string;
};

export function LoadingScreen({ message = "Cargando..." }: LoadingScreenProps) {
  return (
    <View style={styles.container}>
      <ActivityIndicator size="large" color={colors.brand.secondary} />
      <Text style={styles.message}>{message}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    alignItems: "center",
    justifyContent: "center",
    backgroundColor: colors.surface.base,
    gap: 16,
    padding: 24
  },
  message: {
    color: colors.text.primary,
    fontSize: 16,
    textAlign: "center"
  }
});
