import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { HintEditor } from "./hint-editor";

vi.mock("./map-location-picker", () => ({
  MapLocationPicker: ({ onChange }: { onChange: (latitude: string, longitude: string) => void }) => (
    <button onClick={() => onChange("10.5", "-66.9")} type="button">
      Elegir punto
    </button>
  ),
}));

describe("HintEditor", () => {
  it("updates both coordinates together when a map point is selected", () => {
    const onLocationUpdate = vi.fn();

    render(
      <HintEditor
        hint={{ clientId: "hint-1", content: "Pista", isSolution: false, latitude: "", longitude: "" }}
        index={0}
        onLocationUpdate={onLocationUpdate}
        onRemove={vi.fn()}
        onUpdate={vi.fn()}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: /agregar ubicaci/i }));
    fireEvent.click(screen.getByRole("button", { name: "Elegir punto" }));

    expect(onLocationUpdate).toHaveBeenCalledWith("10.5", "-66.9");
  });
});
