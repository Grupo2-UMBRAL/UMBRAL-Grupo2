import { Redirect } from "expo-router";

/** The Expo web server opens `/`; the participant shell is intentionally namespaced at `/mobile`. */
export default function WebEntryPage() {
  return <Redirect href="/mobile" />;
}
