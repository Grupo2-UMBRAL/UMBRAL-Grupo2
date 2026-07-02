import type { ReactNode } from "react";
import { ScrollView, StyleSheet, Text, View } from "react-native";
import { SafeAreaView } from "react-native-safe-area-context";
import { colors } from "../theme/tokens";

type ScreenShellProps = {
  eyebrow?: string;
  title: string;
  description?: string;
  children: ReactNode;
};

export function ScreenShell({
  eyebrow,
  title,
  description,
  children
}: ScreenShellProps) {
  return (
    <SafeAreaView style={styles.safeArea} edges={["top", "left", "right"]}>
      <ScrollView contentContainerStyle={styles.content}>
        <View style={styles.hero}>
          {eyebrow ? <Text style={styles.eyebrow}>{eyebrow}</Text> : null}
          <Text style={styles.title}>{title}</Text>
          {description ? <Text style={styles.description}>{description}</Text> : null}
        </View>
        {children}
      </ScrollView>
    </SafeAreaView>
  );
}

export const shellStyles = StyleSheet.create({
  card: {
    backgroundColor: colors.surface.card,
    borderRadius: 24,
    padding: 20,
    gap: 12,
    borderWidth: 1,
    borderColor: colors.surface.cardBorder
  },
  cardTitle: {
    color: colors.text.primary,
    fontSize: 18,
    fontWeight: "700"
  },
  cardText: {
    color: colors.text.secondary,
    fontSize: 15,
    lineHeight: 22
  },
  row: {
    flexDirection: "row",
    flexWrap: "wrap",
    gap: 12
  },
  section: {
    gap: 12
  },
  list: {
    gap: 10
  },
  mono: {
    color: colors.text.primary,
    fontFamily: "monospace",
    fontSize: 13
  }
});

const styles = StyleSheet.create({
  safeArea: {
    flex: 1,
    backgroundColor: colors.surface.base
  },
  content: {
    padding: 20,
    gap: 20
  },
  hero: {
    gap: 10
  },
  eyebrow: {
    color: colors.brand.secondary,
    fontSize: 12,
    fontWeight: "700",
    letterSpacing: 1.2,
    textTransform: "uppercase"
  },
  title: {
    color: colors.text.primary,
    fontSize: 32,
    fontWeight: "800",
    lineHeight: 38
  },
  description: {
    color: colors.text.secondary,
    fontSize: 16,
    lineHeight: 24
  }
});
