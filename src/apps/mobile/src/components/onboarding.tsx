import { useEffect, useRef, useState } from "react";
import { Animated, Easing, Modal, Pressable, StyleSheet, Text, View } from "react-native";
import { SafeAreaView } from "react-native-safe-area-context";
import { GameButton } from "./game-button";
import { Mascot, type MascotMood } from "./mascot";
import { colors, radius, space } from "../theme/tokens";

/**
 * Entrada de un participante nuevo, en dos piezas que no compiten:
 *
 * - `OnboardingTutorial` — el evento de primera vez. Bloquea, se salta, y ocurre al ENTRAR A HOME,
 *   no al unirse a un equipo. Consecuencia asumida: cuenta la dinámica antes de que exista una
 *   etapa donde colgarla, a cambio de que ocurra sí o sí en vez de depender de que el jugador
 *   llegue a inscribirse.
 * - `OnboardingChecklist` — el mueble permanente. No interrumpe; queda disponible hasta que se
 *   descarta.
 *
 * Vocabulario obligatorio: etapa / misión / sesión. Nunca "sección" ni "play".
 */

type TutorialStep = {
  key: string;
  icon: string;
  mood: MascotMood;
  /** Golpe corto antes del título. El checklist lo ignora: ahí manda la lista. */
  kicker: string;
  title: string;
  body: string;
};

/**
 * La dinámica en cuatro golpes. Todo lo afirmado sale del código, no del entusiasmo: el puntaje
 * base es el de `basePointsForDifficulty` (ERS: Fácil 100 / Media 200 / Difícil 300), las pistas se
 * desbloquean por regla o las abre el operador y no cuestan puntos (`formatUnlockReason`), y la
 * próxima etapa "se revela al avanzar" (`buildStageProgress`).
 */
const tutorialSteps: TutorialStep[] = [
  {
    key: "join",
    icon: "🚪",
    mood: "waiting",
    kicker: "Primero lo primero",
    title: "¡No hay misterio que se resuelva solo!",
    body: "Pídele el código de sesión a tu operador y arma equipo. Desde ahí juegan como uno: lo que resuelve cualquiera, lo ganan todos."
  },
  {
    key: "stages",
    icon: "🧭",
    mood: "happy",
    kicker: "Así se juega",
    title: "La misión va por etapas",
    body: "Una a una, en orden. Unas te preguntan y respondes desde aquí. Otras te sacan al mundo real a encontrar el lugar exacto. La siguiente sólo se revela cuando superan la actual."
  },
  {
    key: "hints",
    icon: "💡",
    mood: "curious",
    kicker: "Cuando haya dudas",
    title: "¡Siempre hay una pista!",
    body: "Algunas se abren solas al avanzar y otras veces el operador será tu mejor aliado. Úsalas a tu merced!"
  },
  {
    key: "score",
    icon: "🏆",
    mood: "celebrate",
    kicker: "Lo que está en juego",
    title: "Cada etapa deja puntos",
    body: "Fácil 100, media 200, difícil 300. El ranking de tu sesión se mueve en vivo — y los otros equipos lo están mirando."
  }
];

const DOT_SIZE = 10;
const DOT_ACTIVE_WIDTH = 28;

/**
 * El punto activo se estira a pill y vira a verde en vez de saltar. `useNativeDriver: false` es la
 * convención que ya siguen answer-feedback.tsx y session-lobby.tsx.
 *
 * Interpolar entre dos tokens planos es una transición, no un degradado: §3 sigue en pie.
 */
function PagerDot({ active, onPress }: { active: boolean; onPress: () => void }) {
  const progress = useRef(new Animated.Value(active ? 1 : 0)).current;

  useEffect(() => {
    const animation = Animated.timing(progress, {
      toValue: active ? 1 : 0,
      duration: 260,
      easing: Easing.out(Easing.cubic),
      useNativeDriver: false
    });

    animation.start();
    return () => animation.stop();
  }, [active, progress]);

  return (
    <Pressable onPress={onPress} hitSlop={8} accessibilityRole="button">
      <Animated.View
        style={{
          height: DOT_SIZE,
          borderRadius: radius.pill,
          width: progress.interpolate({ inputRange: [0, 1], outputRange: [DOT_SIZE, DOT_ACTIVE_WIDTH] }),
          backgroundColor: progress.interpolate({
            inputRange: [0, 1],
            outputRange: [colors.surface.cardBorder, colors.brand.primary]
          })
        }}
      />
    </Pressable>
  );
}

/**
 * La burbuja de duda flota: sube y baja en loop suave para que el "?" se sienta vivo, no pegado.
 * `useNativeDriver: false` por la convención del archivo (translateY va perfecto igual).
 */
function DoubtBubble() {
  const float = useRef(new Animated.Value(0)).current;

  useEffect(() => {
    const loop = Animated.loop(
      Animated.sequence([
        Animated.timing(float, { toValue: 1, duration: 1100, easing: Easing.inOut(Easing.quad), useNativeDriver: false }),
        Animated.timing(float, { toValue: 0, duration: 1100, easing: Easing.inOut(Easing.quad), useNativeDriver: false })
      ])
    );

    loop.start();
    return () => loop.stop();
  }, [float]);

  const translateY = float.interpolate({ inputRange: [0, 1], outputRange: [0, -9] });

  return (
    <Animated.View style={[styles.doubtBubble, { transform: [{ translateY }] }]}>
      <Text style={styles.doubtMark}>?</Text>
    </Animated.View>
  );
}

export function OnboardingTutorial({ onDone }: { onDone: () => void }) {
  const [index, setIndex] = useState(0);
  const step = tutorialSteps[index];
  const isLast = index === tutorialSteps.length - 1;

  const opacity = useRef(new Animated.Value(1)).current;
  const translate = useRef(new Animated.Value(0)).current;
  // Sin cerrojo, dos toques rápidos dejan el contenido a medio camino: el callback de la salida de
  // un paso pisaría la entrada del siguiente.
  const busy = useRef(false);

  /**
   * Sale hacia donde vas y el paso nuevo entra desde el lado contrario, así el movimiento dice la
   * dirección. Se anima `translateX` en px directo en vez de interpolar: salida y entrada van a
   * lados opuestos, y un solo interpolate no puede cambiar de signo a mitad.
   */
  const goTo = (next: number) => {
    if (busy.current || next === index || next < 0 || next >= tutorialSteps.length) {
      return;
    }

    const direction = next > index ? 1 : -1;
    busy.current = true;

    Animated.parallel([
      Animated.timing(opacity, { toValue: 0, duration: 140, easing: Easing.in(Easing.quad), useNativeDriver: false }),
      Animated.timing(translate, { toValue: -direction * 28, duration: 140, easing: Easing.in(Easing.quad), useNativeDriver: false })
    ]).start(() => {
      setIndex(next);
      translate.setValue(direction * 28);

      Animated.parallel([
        Animated.timing(opacity, { toValue: 1, duration: 240, easing: Easing.out(Easing.cubic), useNativeDriver: false }),
        Animated.timing(translate, { toValue: 0, duration: 240, easing: Easing.out(Easing.cubic), useNativeDriver: false })
      ]).start(() => {
        busy.current = false;
      });
    });
  };

  return (
    <Modal animationType="fade" transparent={false} visible onRequestClose={onDone}>
      <SafeAreaView style={styles.screen}>
        <View style={styles.topBar}>
          <Text style={styles.counter}>
            {index + 1} de {tutorialSteps.length}
          </Text>
          <Pressable onPress={onDone} hitSlop={12} accessibilityRole="button">
            <Text style={styles.skip}>Saltar</Text>
          </Pressable>
        </View>

        <Animated.View style={[styles.stage, { opacity, transform: [{ translateX: translate }] }]}>
          <View style={styles.mascotHalo}>
            <Mascot mood={step.mood} size={160} winkSignal={index} />
            {/* La duda vive en el paso de pistas: burbuja "?" al vuelo, fuera de la mascota para no
                romper su lenguaje de formas (§12 no admite glifos dentro del personaje). */}
            {step.mood === "curious" ? <DoubtBubble /> : null}
          </View>
          <Text style={styles.icon}>{step.icon}</Text>
          <Text style={styles.kicker}>{step.kicker}</Text>
          <Text style={styles.title}>{step.title}</Text>
          <Text style={styles.body}>{step.body}</Text>
        </Animated.View>

        <View style={styles.dots}>
          {tutorialSteps.map((candidate, dotIndex) => (
            <PagerDot key={candidate.key} active={dotIndex === index} onPress={() => goTo(dotIndex)} />
          ))}
        </View>

        <View style={styles.footer}>
          {index > 0 ? (
            <GameButton label="Atrás" variant="ghost" onPress={() => goTo(index - 1)} />
          ) : null}
          <GameButton
            label={isLast ? "¡EMPECEMOS!" : "Siguiente"}
            icon={isLast ? "🎮" : "→"}
            onPress={() => (isLast ? onDone() : goTo(index + 1))}
          />
        </View>
      </SafeAreaView>
    </Modal>
  );
}

export function OnboardingChecklist({ onDismiss }: { onDismiss: () => void }) {
  const [expanded, setExpanded] = useState(false);
  const [openKey, setOpenKey] = useState<string | null>(null);
  const [readKeys, setReadKeys] = useState<string[]>([]);

  const markRead = (key: string) => {
    setOpenKey((current) => (current === key ? null : key));
    setReadKeys((current) => (current.includes(key) ? current : [...current, key]));
  };

  if (!expanded) {
    return (
      <Pressable
        onPress={() => setExpanded(true)}
        accessibilityRole="button"
        style={({ pressed }) => [styles.pill, pressed && styles.pressed]}
      >
        <Text style={styles.pillIcon}>🎮</Text>
        <Text style={styles.pillLabel}>¿Cómo se juega?</Text>
        <Text style={styles.pillCount}>
          {readKeys.length}/{tutorialSteps.length}
        </Text>
      </Pressable>
    );
  }

  return (
    <View style={styles.sheet}>
      <View style={styles.grabber} />
      <View style={styles.sheetHead}>
        <Text style={styles.sheetTitle}>Cómo se juega UMBRAL</Text>
        <Pressable onPress={() => setExpanded(false)} hitSlop={12} accessibilityRole="button">
          <Text style={styles.collapse}>Minimizar</Text>
        </Pressable>
      </View>

      <View style={styles.list}>
        {tutorialSteps.map((step) => {
          const isRead = readKeys.includes(step.key);
          const isOpen = openKey === step.key;

          return (
            <Pressable
              key={step.key}
              onPress={() => markRead(step.key)}
              accessibilityRole="button"
              style={({ pressed }) => [styles.row, isOpen && styles.rowOpen, pressed && styles.pressed]}
            >
              <View style={styles.rowHead}>
                <View style={[styles.check, isRead && styles.checkDone]}>
                  <Text style={[styles.checkMark, isRead && styles.checkMarkDone]}>
                    {isRead ? "✓" : step.icon}
                  </Text>
                </View>
                <Text style={[styles.rowTitle, isRead && styles.rowTitleRead]}>{step.title}</Text>
                <Text style={styles.chevron}>{isOpen ? "▾" : "▸"}</Text>
              </View>
              {isOpen ? <Text style={styles.rowBody}>{step.body}</Text> : null}
            </Pressable>
          );
        })}
      </View>

      <Pressable onPress={onDismiss} hitSlop={10} accessibilityRole="button" style={styles.dismiss}>
        <Text style={styles.dismissLabel}>No volver a mostrar</Text>
      </Pressable>
    </View>
  );
}

const styles = StyleSheet.create({
  screen: {
    flex: 1,
    backgroundColor: colors.surface.base,
    padding: space.xl
  },
  topBar: {
    alignItems: "center",
    flexDirection: "row",
    justifyContent: "space-between",
    paddingVertical: space.sm
  },
  counter: {
    color: colors.text.mutedAlt,
    fontSize: 13,
    fontWeight: "800",
    letterSpacing: 0.6
  },
  skip: {
    color: colors.text.secondary,
    fontSize: 15,
    fontWeight: "800"
  },
  stage: {
    alignItems: "center",
    flex: 1,
    gap: space.lg,
    justifyContent: "center"
  },
  mascotHalo: {
    alignItems: "center",
    backgroundColor: colors.brand.primaryTint,
    borderRadius: radius.pill,
    height: 236,
    justifyContent: "center",
    position: "relative",
    width: 236
  },
  // Sin burbuja: sólo el "?" suelto flotando arriba a la derecha del halo.
  doubtBubble: {
    alignItems: "center",
    height: 52,
    justifyContent: "center",
    position: "absolute",
    right: 18,
    top: 18,
    width: 52
  },
  doubtMark: {
    color: colors.text.primary,
    fontSize: 45,
    fontWeight: "900"
  },
  icon: {
    fontSize: 34
  },
  kicker: {
    color: colors.brand.secondary,
    fontSize: 12,
    fontWeight: "800",
    letterSpacing: 1.2,
    textTransform: "uppercase"
  },
  title: {
    color: colors.text.primary,
    fontSize: 30,
    fontWeight: "900",
    lineHeight: 36,
    textAlign: "center"
  },
  body: {
    color: colors.text.secondary,
    fontSize: 17,
    lineHeight: 26,
    maxWidth: 340,
    textAlign: "center"
  },
  dots: {
    flexDirection: "row",
    gap: space.sm,
    justifyContent: "center",
    paddingVertical: space.xl
  },
  footer: {
    gap: space.md
  },
  pill: {
    alignItems: "center",
    backgroundColor: colors.brand.secondary,
    borderBottomColor: colors.brand.secondaryRing,
    borderBottomWidth: 3,
    borderRadius: radius.pill,
    bottom: 20,
    flexDirection: "row",
    gap: space.sm,
    left: space.xl,
    paddingHorizontal: space.xl,
    paddingVertical: space.md,
    position: "absolute"
  },
  pillIcon: {
    fontSize: 16
  },
  pillLabel: {
    color: colors.text.onBrand,
    fontSize: 15,
    fontWeight: "900"
  },
  pillCount: {
    color: colors.brand.secondaryTint,
    fontSize: 13,
    fontWeight: "800"
  },
  pressed: {
    opacity: 0.9
  },
  // Despegada del borde: pegada abajo tapaba el "Cerrar sesión" de home, que es el último elemento
  // del scroll. La profundidad es el labio inferior sólido, nunca una sombra difusa (§3).
  sheet: {
    backgroundColor: colors.surface.card,
    borderColor: colors.surface.cardBorder,
    borderRadius: 28,
    borderWidth: 1,
    borderBottomColor: colors.surface.cardBorder,
    borderBottomWidth: 4,
    bottom: 20,
    gap: space.lg,
    left: space.lg,
    paddingBottom: space.lg,
    paddingHorizontal: space.xl,
    paddingTop: space.md,
    position: "absolute",
    right: space.lg
  },
  grabber: {
    alignSelf: "center",
    backgroundColor: colors.surface.cardBorder,
    borderRadius: radius.pill,
    height: 5,
    width: 44
  },
  sheetHead: {
    alignItems: "center",
    flexDirection: "row",
    justifyContent: "space-between"
  },
  sheetTitle: {
    color: colors.text.primary,
    fontSize: 20,
    fontWeight: "900"
  },
  collapse: {
    color: colors.brand.secondary,
    fontSize: 14,
    fontWeight: "800"
  },
  list: {
    gap: space.sm
  },
  row: {
    backgroundColor: colors.surface.raised,
    borderColor: colors.surface.raisedBorder,
    borderRadius: radius.md,
    borderWidth: 1,
    gap: space.sm,
    padding: space.md
  },
  rowOpen: {
    backgroundColor: colors.brand.secondaryTint,
    borderColor: colors.brand.secondary
  },
  rowHead: {
    alignItems: "center",
    flexDirection: "row",
    gap: space.md
  },
  check: {
    alignItems: "center",
    backgroundColor: colors.surface.card,
    borderColor: colors.surface.cardBorder,
    borderRadius: radius.pill,
    borderWidth: 1,
    height: 32,
    justifyContent: "center",
    width: 32
  },
  checkDone: {
    backgroundColor: colors.brand.primary,
    borderColor: colors.brand.primary
  },
  checkMark: {
    fontSize: 14
  },
  checkMarkDone: {
    color: colors.text.onBrand,
    fontWeight: "900"
  },
  rowTitle: {
    color: colors.text.primary,
    flex: 1,
    fontSize: 15,
    fontWeight: "800"
  },
  rowTitleRead: {
    color: colors.text.secondary
  },
  chevron: {
    color: colors.text.mutedAlt,
    fontSize: 14
  },
  rowBody: {
    color: colors.text.secondary,
    fontSize: 14,
    lineHeight: 21,
    paddingLeft: 44
  },
  dismiss: {
    alignItems: "center",
    paddingVertical: space.sm
  },
  dismissLabel: {
    color: colors.text.mutedAlt,
    fontSize: 14,
    fontWeight: "700"
  }
});
