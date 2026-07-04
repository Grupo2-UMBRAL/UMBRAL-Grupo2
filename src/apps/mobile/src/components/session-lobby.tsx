import { useEffect, useRef } from "react";
import { Animated, Easing, StyleSheet, Text, View } from "react-native";
import { Mascot } from "./mascot";
import { StatusChip } from "./status-chip";
import { colors } from "../theme/tokens";

/**
 * Waiting room shown after a player joins a team but before the operator starts the session.
 * Replaces the raw "Awaiting lifecycle event" board with a calm, alive holding screen that shows
 * the team roster filling up. It leaves on its own when the board's SignalR / poll flips the
 * session to Active.
 */
type SessionLobbyProps = {
  teamName: string;
  memberCount: number;
  connectionLabel: string;
  connectionTone: "neutral" | "info" | "success" | "warn" | "error";
};

const AVATAR_TONES = [colors.brand.primary, colors.brand.secondary, colors.state.warn.textAlt];
const MAX_AVATARS = 8;

export function SessionLobby({ teamName, memberCount, connectionLabel, connectionTone }: SessionLobbyProps) {
  const pulse = useRef(new Animated.Value(0)).current;
  const dots = useRef(new Animated.Value(0)).current;

  useEffect(() => {
    const bounce = Animated.loop(
      Animated.sequence([
        Animated.timing(pulse, { toValue: 1, duration: 900, easing: Easing.inOut(Easing.ease), useNativeDriver: false }),
        Animated.timing(pulse, { toValue: 0, duration: 900, easing: Easing.inOut(Easing.ease), useNativeDriver: false })
      ])
    );
    const blink = Animated.loop(
      Animated.timing(dots, { toValue: 3, duration: 1500, easing: Easing.linear, useNativeDriver: false })
    );
    bounce.start();
    blink.start();

    return () => {
      bounce.stop();
      blink.stop();
    };
  }, [pulse, dots]);

  const scale = pulse.interpolate({ inputRange: [0, 1], outputRange: [1, 1.08] });
  const translateY = pulse.interpolate({ inputRange: [0, 1], outputRange: [0, -6] });
  const safeCount = Math.max(1, memberCount);
  const shownAvatars = Math.min(safeCount, MAX_AVATARS);
  const overflow = safeCount - shownAvatars;

  return (
    <View style={styles.card}>
      <Animated.View style={{ transform: [{ scale }, { translateY }] }}>
        <Mascot mood="waiting" size={120} />
      </Animated.View>

      <Text style={styles.title}>¡Ya casi empezamos!</Text>
      <Text style={styles.subtitle}>
        Estás dentro con <Text style={styles.team}>{teamName}</Text>. Esperando a que el operador inicie la
        sesión…
      </Text>

      <View style={styles.roster}>
        <View style={styles.avatarRow}>
          {Array.from({ length: shownAvatars }).map((_, index) => (
            <View
              key={index}
              style={[
                styles.avatar,
                { backgroundColor: AVATAR_TONES[index % AVATAR_TONES.length], marginLeft: index === 0 ? 0 : -10 }
              ]}
            >
              <View style={styles.avatarHead} />
            </View>
          ))}
          {overflow > 0 ? (
            <View style={[styles.avatar, styles.avatarMore]}>
              <Text style={styles.avatarMoreText}>+{overflow}</Text>
            </View>
          ) : null}
        </View>
        <Text style={styles.rosterLabel}>
          {safeCount === 1 ? "1 jugador en tu equipo" : `${safeCount} jugadores en tu equipo`}
        </Text>
      </View>

      <View style={styles.dotsRow}>
        {[0, 1, 2].map((index) => {
          const opacity = dots.interpolate({
            inputRange: [index, index + 0.5, index + 1, 3],
            outputRange: [0.25, 1, 0.25, 0.25],
            extrapolate: "clamp"
          });
          return <Animated.View key={index} style={[styles.dot, { opacity }]} />;
        })}
      </View>

      <StatusChip label={connectionLabel} tone={connectionTone} />
    </View>
  );
}

const styles = StyleSheet.create({
  card: {
    alignItems: "center",
    backgroundColor: colors.surface.card,
    borderColor: colors.surface.cardBorder,
    borderRadius: 26,
    borderWidth: 1,
    gap: 14,
    paddingHorizontal: 22,
    paddingVertical: 32
  },
  title: {
    color: colors.text.primary,
    fontSize: 24,
    fontWeight: "900",
    textAlign: "center"
  },
  subtitle: {
    color: colors.text.secondary,
    fontSize: 16,
    lineHeight: 23,
    textAlign: "center"
  },
  team: {
    color: colors.brand.secondary,
    fontWeight: "800"
  },
  roster: {
    alignItems: "center",
    gap: 8
  },
  avatarRow: {
    flexDirection: "row"
  },
  avatar: {
    alignItems: "center",
    borderColor: colors.surface.card,
    borderRadius: 999,
    borderWidth: 2,
    height: 36,
    justifyContent: "center",
    width: 36
  },
  avatarHead: {
    backgroundColor: colors.text.onBrand,
    borderRadius: 999,
    height: 12,
    width: 12
  },
  avatarMore: {
    backgroundColor: colors.surface.raised,
    borderColor: colors.surface.cardBorder,
    marginLeft: -10
  },
  avatarMoreText: {
    color: colors.text.secondary,
    fontSize: 12,
    fontWeight: "800"
  },
  rosterLabel: {
    color: colors.text.primary,
    fontSize: 14,
    fontWeight: "800"
  },
  dotsRow: {
    flexDirection: "row",
    gap: 8
  },
  dot: {
    backgroundColor: colors.brand.secondary,
    borderRadius: 999,
    height: 10,
    width: 10
  }
});
