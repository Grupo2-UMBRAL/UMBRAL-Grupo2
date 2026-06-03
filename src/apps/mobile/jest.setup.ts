// Jest setup reserved for mobile tests.

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
