import { useNavigate } from "@tanstack/react-router";
import { useStore, standTypeColor, type Stand } from "@/lib/store";
import { useShallow } from "zustand/react/shallow";
import { Beer, UtensilsCrossed, IceCream, Wine, Music, DoorOpen, Crown, Bath, Wallet } from "lucide-react";
import { useEffect, useMemo, useRef, useState } from "react";

const MIN_MAP_WIDTH = 800;
const MIN_MAP_HEIGHT = 520;
const MAP_MARGIN = 40;

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
  const [viewportWidth, setViewportWidth] = useState(0);
  const [drag, setDrag] = useState<{ id: string; ox: number; oy: number } | null>(null);

  const mapSize = useMemo(() => ({
    width: Math.max(MIN_MAP_WIDTH, ...stands.map((stand) => stand.x + stand.width + MAP_MARGIN)),
    height: Math.max(MIN_MAP_HEIGHT, ...stands.map((stand) => stand.y + stand.height + MAP_MARGIN)),
  }), [stands]);
  const scale = viewportWidth > 0 ? Math.min(1, viewportWidth / mapSize.width) : 1;

  useEffect(() => {
    const element = ref.current;
    if (!element) return;
    const updateWidth = () => setViewportWidth(element.clientWidth);
    updateWidth();
    const observer = new ResizeObserver(updateWidth);
    observer.observe(element);
    return () => observer.disconnect();
  }, []);

  const onDown = (e: React.PointerEvent, s: Stand) => {
    if (!editable) return;
    e.stopPropagation();
    (e.currentTarget as HTMLElement).setPointerCapture(e.pointerId);
    const bounds = ref.current?.getBoundingClientRect();
    if (!bounds) return;
    const pointerX = (e.clientX - bounds.left) / scale;
    const pointerY = (e.clientY - bounds.top) / scale;
    setDrag({ id: s.id, ox: pointerX - s.x, oy: pointerY - s.y });
  };
  const onMove = (e: React.PointerEvent) => {
    if (!drag) return;
    const bounds = ref.current?.getBoundingClientRect();
    if (!bounds) return;
    const snap = (v: number) => Math.round(v / 20) * 20;
    const pointerX = (e.clientX - bounds.left) / scale;
    const pointerY = (e.clientY - bounds.top) / scale;
    moveStand(drag.id, Math.max(0, snap(pointerX - drag.ox)), Math.max(0, snap(pointerY - drag.oy)));
  };
  const onUp = () => setDrag(null);

  return (
    <div
      ref={ref}
      onPointerMove={onMove}
      onPointerUp={onUp}
      onPointerCancel={onUp}
      className="relative w-full overflow-hidden rounded-lg border border-border bg-card shadow-elevated touch-none"
      style={{ height: viewportWidth > 0 ? mapSize.height * scale : mapSize.height }}
    >
      <div
        className="absolute left-0 top-0 origin-top-left"
        style={{
          width: mapSize.width,
          height: mapSize.height,
          transform: `scale(${scale})`,
          backgroundImage:
            "linear-gradient(oklch(0.3 0.02 50 / 0.5) 1px, transparent 1px), linear-gradient(90deg, oklch(0.3 0.02 50 / 0.5) 1px, transparent 1px)",
          backgroundSize: "20px 20px",
        }}
      >
        {stands.map((s) => {
        const Icon = ICONS[s.category];
        const isSel = selectedId === s.id;
        const color = standTypeColor[s.category];
        const linkedPartner = partners.find((p) => p.standId === s.id);
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
            {linkedPartner && (
              <span className="absolute -top-2 -right-2 w-3 h-3 rounded-full bg-success border-2 border-background" title={`Parceiro: ${linkedPartner.companyName}`} />
            )}
            <Icon className="w-5 h-5" style={{ color }} />
            <span className="text-xs font-semibold text-foreground px-1 text-center leading-tight">{s.name}</span>
            {editable && linkedPartner && (
              <span className="text-[10px] text-success/90 truncate max-w-full px-1">● {linkedPartner.companyName}</span>
            )}
          </div>
        );
        })}
      </div>
    </div>
  );
}
