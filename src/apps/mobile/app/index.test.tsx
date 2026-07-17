import { render, screen } from "@testing-library/react-native";
import WebEntryPage from "./index";

jest.mock("expo-router", () => ({
  Redirect: ({ href }: { href: string }) => {
    const React = require("react");
    const { Text } = require("react-native");

    return React.createElement(Text, null, href);
  }
}));

test("redirects the web root to the participant shell", () => {
  render(<WebEntryPage />);

  expect(screen.getByText("/mobile")).toBeTruthy();
});
