import { Redirect } from "expo-router";
import { useState } from "react";
import { EntrySplash } from "../../src/components/entry-splash";
import { LoadingScreen } from "../../src/components/loading-screen";
import { useSession } from "../../src/providers/session-provider";

// Un lanzamiento = un splash. `index` es el punto de entrada, pero puede remontarse (p.ej. tras
// cerrar sesión vuelve a pasar por aquí); este flag a nivel de módulo mantiene la animación como un
// evento de arranque, no un parpadeo en cada redirección.
let introPlayed = false;

export default function IndexPage() {
  const { loading, session } = useSession();
  const [introDone, setIntroDone] = useState(introPlayed);

  if (!introDone) {
    return (
      <EntrySplash
        onDone={() => {
          introPlayed = true;
          setIntroDone(true);
        }}
      />
    );
  }

  if (loading) {
    return <LoadingScreen />;
  }

  if (!session) {
    return <Redirect href="/mobile/login" />;
  }

  if (!session.roles.includes("Participant")) {
    return <Redirect href="/mobile/forbidden" />;
  }

  return <Redirect href="/mobile/home" />;
}
