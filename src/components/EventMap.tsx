import { useNavigate } from "@tanstack/react-router";
import { useStore, standTypeColor, type Stand } from "@/lib/store";
import { useShallow } from "zustand/react/shallow";
import { Beer, UtensilsCrossed, IceCream, Wine, Music, DoorOpen, Crown, Bath, Wallet } from "lucide-react";
import { useRef, useState } from "react";

const ICONS = {
  food: UtensilsCrossed, drink: Wine, beer: Beer, dessert: IceCream,
  stage: Music, entrance: DoorOpen, vip: Crown, restroom: Bath, cashier: Wallet,
} as const;

interface Props {
  eventId: string;
  editable?: boolean;
  onSelect?: (s: Stand) => void;
  selectedId?: string;
}

export function EventMap({ eventId, editable, onSelect, selectedId }: Props) {
  const stands = useStore(useShallow((s) => s.stands.filter((x) => x.eventId === eventId)));
  const partners = useStore(useShallow((s) => s.partners.filter((p) => p.eventId === eventId)));
  const moveStand = useStore((s) => s.moveStand);
  const navigate = useNavigate();
  const ref = useRef<HTMLDivElement>(null);
  const [drag, setDrag] = useState<{ id: string; ox: number; oy: number } | null>(null);

  const onDown = (e: React.PointerEvent, s: Stand) => {
    if (!editable) return;
    e.stopPropagation();
    (e.currentTarget as HTMLElement).setPointerCapture(e.pointerId);
    setDrag({ id: s.id, ox: e.clientX - s.x, oy: e.clientY - s.y });
  };
  const onMove = (e: React.PointerEvent) => {
    if (!drag) return;
    const snap = (v: number) => Math.round(v / 20) * 20;
    moveStand(drag.id, Math.max(0, snap(e.clientX - drag.ox)), Math.max(0, snap(e.clientY - drag.oy)));
  };
  const onUp = () => setDrag(null);

  return (
    <div
      ref={ref}
      onPointerMove={onMove}
      onPointerUp={onUp}
      className="relative w-full h-[520px] rounded-2xl border border-border overflow-hidden bg-card shadow-elevated"
      style={{
        backgroundImage:
          "linear-gradient(oklch(0.3 0.02 50 / 0.5) 1px, transparent 1px), linear-gradient(90deg, oklch(0.3 0.02 50 / 0.5) 1px, transparent 1px)",
        backgroundSize: "20px 20px",
      }}
    >
      {stands.map((s) => {
        const Icon = ICONS[s.category];
        const isSel = selectedId === s.id;
        const color = standTypeColor[s.category];
        return (
          <div
            key={s.id}
            onPointerDown={(e) => onDown(e, s)}
            onClick={() => {
              if (editable) onSelect?.(s);
              else if (["food", "drink", "beer", "dessert"].includes(s.category))
                navigate({ to: "/events/$eventId/stands/$standId", params: { eventId, standId: s.id } });
            }}
            className={`absolute flex flex-col items-center justify-center gap-1 rounded-xl border-2 transition-all ${
              editable ? "cursor-move" : "cursor-pointer hover:scale-[1.04]"
            } ${isSel ? "ring-2 ring-primary ring-offset-2 ring-offset-background" : ""}`}
            style={{
              left: s.x, top: s.y, width: s.width, height: s.height,
              backgroundColor: `color-mix(in oklab, ${color} 25%, transparent)`,
              borderColor: color,
              boxShadow: `0 8px 24px -8px ${color}`,
            }}
          >
            <Icon className="w-5 h-5" style={{ color }} />
            <span className="text-xs font-semibold text-foreground px-1 text-center leading-tight">{s.name}</span>
          </div>
        );
      })}
    </div>
  );
}
