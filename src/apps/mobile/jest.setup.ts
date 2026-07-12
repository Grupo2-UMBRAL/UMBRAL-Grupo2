// Jest setup reserved for mobile tests.

jest.mock("react-native-webview", () => {
  const React = require("react");
  const { View } = require("react-native");

  return {
    WebView: ({ children, ...props }: any) =>
      React.createElement(View, { ...props, testID: "web-view" }, children)
  };
});

jest.mock("expo-location", () => ({
  Accuracy: { Balanced: 3 },
  requestForegroundPermissionsAsync: jest
    .fn()
    .mockResolvedValue({ status: "granted", granted: true }),
  getCurrentPositionAsync: jest
    .fn()
    .mockResolvedValue({ coords: { latitude: 10.5, longitude: -66.9 } })
}));

jest.mock("expo-camera", () => {
  const React = require("react");
  const { View } = require("react-native");

  return {
    CameraView: ({ children, ...props }: any) =>
      React.createElement(View, { ...props, testID: "camera-view" }, children),
    useCameraPermissions: jest.fn(() => [
      { granted: true, status: "granted" },
      jest.fn().mockResolvedValue({ granted: true, status: "granted" })
    ])
  };
});
