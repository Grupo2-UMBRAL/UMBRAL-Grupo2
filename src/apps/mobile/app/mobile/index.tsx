import { Redirect } from "expo-router";
import { LoadingScreen } from "../../src/components/loading-screen";
import { useSession } from "../../src/providers/session-provider";

export default function IndexPage() {
  const { loading, session } = useSession();

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
