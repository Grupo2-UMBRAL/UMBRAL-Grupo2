import { ActivityIndicator, StyleSheet, Text, View } from "react-native";

type LoadingScreenProps = {
  message?: string;
};

export function LoadingScreen({ message = "Loading mobile shell..." }: LoadingScreenProps) {
  return (
    <View style={styles.container}>
      <ActivityIndicator size="large" color="#1e6f8c" />
      <Text style={styles.message}>{message}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    alignItems: "center",
    justifyContent: "center",
    backgroundColor: "#f6efe6",
    gap: 16,
    padding: 24
  },
  message: {
    color: "#17313b",
    fontSize: 16,
    textAlign: "center"
  }
});
