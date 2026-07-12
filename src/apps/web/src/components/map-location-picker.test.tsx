import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { MapLocationPicker } from "./map-location-picker";

// The Maps SDK is mocked so tests never touch the network. importLibrary
// returns a promise that never resolves, keeping the picker in its "loading"
// state so the init effect (which needs the real google.maps global) never runs.
const { setOptions, importLibrary } = vi.hoisted(() => ({
  setOptions: vi.fn(),
  importLibrary: vi.fn(() => new Promise(() => {})),
}));
vi.mock("@googlemaps/js-api-loader", () => ({ setOptions, importLibrary }));

const { getClientConfig } = vi.hoisted(() => ({ getClientConfig: vi.fn() }));
vi.mock("../lib/config", () => ({ getClientConfig }));

describe("MapLocationPicker", () => {
  beforeEach(() => {
    setOptions.mockClear();
    importLibrary.mockClear();
  });

  it("renders nothing and never loads the SDK when no api key is configured", () => {
    getClientConfig.mockReturnValue({ googleMapsApiKey: "" });
    const onChange = vi.fn();

    const { container } = render(
      <MapLocationPicker latitude="" longitude="" onChange={onChange} />,
    );

    // Falls back to the manual lat/lng inputs owned by hint-editor: the picker
    // itself contributes no DOM and never touches Google.
    expect(container).toBeEmptyDOMElement();
    expect(setOptions).not.toHaveBeenCalled();
    expect(importLibrary).not.toHaveBeenCalled();
    expect(onChange).not.toHaveBeenCalled();
  });

  it("shows the place search box and loads the SDK when a key is configured", () => {
    getClientConfig.mockReturnValue({ googleMapsApiKey: "test-key" });

    render(<MapLocationPicker latitude="" longitude="" onChange={vi.fn()} />);

    expect(screen.getByPlaceholderText(/buscar un lugar/i)).toBeInTheDocument();
    expect(setOptions).toHaveBeenCalledWith({ key: "test-key", v: "weekly" });
    expect(importLibrary).toHaveBeenCalledWith("maps");
    expect(importLibrary).toHaveBeenCalledWith("places");
  });
});
