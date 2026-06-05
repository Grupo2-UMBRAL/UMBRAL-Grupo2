// Learn more https://docs.expo.dev/guides/customizing-metro
const { getDefaultConfig } = require("expo/metro-config");

/** @type {import('expo/metro-config').MetroConfig} */
const config = getDefaultConfig(__dirname);

// Exclude test files from the bundle so Metro does not try to resolve
// test-only dependencies (jest, @testing-library, react-test-renderer, etc.)
config.resolver.blockList = [
  ...(Array.isArray(config.resolver.blockList)
    ? config.resolver.blockList
    : config.resolver.blockList
      ? [config.resolver.blockList]
      : []),
  /\.test\.[jt]sx?$/,
  /\.spec\.[jt]sx?$/,
  /jest\.setup\.[jt]s$/,
];

module.exports = config;
