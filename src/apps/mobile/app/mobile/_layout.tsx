import { Stack } from "expo-router";
import { StatusBar } from "expo-status-bar";
import { Buffer } from "buffer";
import { SessionProvider } from "../../src/providers/session-provider";

if (!globalThis.Buffer) {
  globalThis.Buffer = Buffer;
}

import { Platform } from 'react-native';
import { WebTracerProvider, BatchSpanProcessor } from '@opentelemetry/sdk-trace-web';
import { MeterProvider, PeriodicExportingMetricReader } from '@opentelemetry/sdk-metrics';
import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-http';
import { OTLPMetricExporter } from '@opentelemetry/exporter-metrics-otlp-http';
import { resourceFromAttributes } from '@opentelemetry/resources';
import { registerInstrumentations } from '@opentelemetry/instrumentation';
import { FetchInstrumentation } from '@opentelemetry/instrumentation-fetch';

const otlpEndpoint = process.env.EXPO_PUBLIC_OTLP_ENDPOINT || (Platform.OS === 'android' ? 'http://10.0.2.2:4318' : 'http://localhost:4318');

const resource = resourceFromAttributes({
  'service.name': 'umbral-mobile',
});

const traceProvider = new WebTracerProvider({
  resource,
  spanProcessors: [
    new BatchSpanProcessor(new OTLPTraceExporter({
      url: `${otlpEndpoint}/v1/traces`,
    }))
  ]
});
traceProvider.register();

const meterProvider = new MeterProvider({
  resource,
  readers: [
    new PeriodicExportingMetricReader({
      exporter: new OTLPMetricExporter({
        url: `${otlpEndpoint}/v1/metrics`,
      }),
      exportIntervalMillis: 10000,
    })
  ]
});

registerInstrumentations({
  instrumentations: [
    new FetchInstrumentation(),
  ],
});

export default function RootLayout() {
  return (
    <SessionProvider>
      <StatusBar style="dark" />
      <Stack
        screenOptions={{
          headerShown: false,
          contentStyle: {
            backgroundColor: "#f6efe6"
          }
        }}
      >
        <Stack.Screen name="index" />
        <Stack.Screen name="login" />
        <Stack.Screen name="forbidden" />
        <Stack.Screen name="(participant)" />
      </Stack>
    </SessionProvider>
  );
}
