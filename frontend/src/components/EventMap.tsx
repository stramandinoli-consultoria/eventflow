import { useNavigate } from "@tanstack/react-router";
import { useStore, standTypeColor, type Stand } from "@/lib/store";
import { useShallow } from "zustand/react/shallow";
import { Beer, UtensilsCrossed, IceCream, Wine, Music, DoorOpen, Crown, Bath, Wallet, ZoomIn, ZoomOut, Maximize2, Hand, Move } from "lucide-react";
import { useEffect, useMemo, useRef, useState } from "react";
import { Button } from "@/components/ui/button";

const MIN_MAP_WIDTH = 800;
const MIN_MAP_HEIGHT = 520;
const MAP_MARGIN = 40;
const MIN_ZOOM = 0.5;
const MAX_ZOOM = 3;

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

const clamp = (v: number, a: number, b: number) => Math.min(b, Math.max(a, v));

export function EventMap({ eventId, editable, onSelect, selectedId }: Props) {
  const stands = useStore(useShallow((s) => s.stands.filter((x) => x.eventId === eventId)));
  const partners = useStore(useShallow((s) => s.partners.filter((p) => p.eventId === eventId)));
  const moveStand = useStore((s) => s.moveStand);
  const navigate = useNavigate();
  const ref = useRef<HTMLDivElement>(null);
  const [viewportWidth, setViewportWidth] = useState(0);
  const [zoom, setZoom] = useState(1);
  const [offset, setOffset] = useState({ x: 0, y: 0 });
  const [mode, setMode] = useState<"pan" | "move">(editable ? "move" : "pan");
  const [drag, setDrag] = useState<{ id: string; ox: number; oy: number } | null>(null);
  const pan = useRef<{ sx: number; sy: number; ox: number; oy: number; moved: boolean } | null>(null);
  const movedRef = useRef(false);

  const mapSize = useMemo(() => ({
    width: Math.max(MIN_MAP_WIDTH, ...stands.map((s) => s.x + s.width + MAP_MARGIN)),
    height: Math.max(MIN_MAP_HEIGHT, ...stands.map((s) => s.y + s.height + MAP_MARGIN)),
  }), [stands]);
  const fit = viewportWidth > 0 ? Math.min(1, viewportWidth / mapSize.width) : 1;
  const scale = fit * zoom;
  const containerH = viewportWidth > 0 ? Math.max(280, mapSize.height * fit) : mapSize.height;

  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    const update = () => setViewportWidth(el.clientWidth);
    update();
    const ob = new ResizeObserver(update);
    ob.observe(el);
    return () => ob.disconnect();
  }, []);

  const zoomAt = (next: number, px: number, py: number) => {
    setZoom((z) => {
      const nz = clamp(next, MIN_ZOOM, MAX_ZOOM);
      const k = nz / z;
      setOffset((o) => ({ x: px - (px - o.x) * k, y: py - (py - o.y) * k }));
      return nz;
    });
  };
  const zoomCenter = (factor: number) => {
    const el = ref.current;
    if (!el) return;
    zoomAt(zoom * factor, el.clientWidth / 2, el.clientHeight / 2);
  };
  const reset = () => { setZoom(1); setOffset({ x: 0, y: 0 }); };

  const wheelRef = useRef<(e: WheelEvent) => void>(() => {});
  wheelRef.current = (e: WheelEvent) => {
    const el = ref.current;
    if (!el) return;
    const r = el.getBoundingClientRect();
    const dy = e.deltaY * (e.deltaMode === 1 ? 16 : e.deltaMode === 2 ? 100 : 1);
    zoomAt(zoom * Math.exp(-dy * 0.0015), e.clientX - r.left, e.clientY - r.top);
  };
  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    const h = (e: WheelEvent) => { e.preventDefault(); wheelRef.current(e); };
    el.addEventListener("wheel", h, { passive: false });
    return () => el.removeEventListener("wheel", h);
  }, []);

  const toLogical = (cx: number, cy: number) => {
    const r = ref.current!.getBoundingClientRect();
    return { x: (cx - r.left - offset.x) / scale, y: (cy - r.top - offset.y) / scale };
  };

  const onStandDown = (e: React.PointerEvent, s: Stand) => {
    if (!editable || mode !== "move") return;
    e.stopPropagation();
    (e.currentTarget as HTMLElement).setPointerCapture(e.pointerId);
    const p = toLogical(e.clientX, e.clientY);
    setDrag({ id: s.id, ox: p.x - s.x, oy: p.y - s.y });
  };
  const onBgDown = (e: React.PointerEvent) => {
    movedRef.current = false;
    pan.current = { sx: e.clientX, sy: e.clientY, ox: offset.x, oy: offset.y, moved: false };
  };
  const onMove = (e: React.PointerEvent) => {
    if (drag) {
      const snap = (v: number) => Math.round(v / 20) * 20;
      const p = toLogical(e.clientX, e.clientY);
      moveStand(drag.id, Math.max(0, snap(p.x - drag.ox)), Math.max(0, snap(p.y - drag.oy)));
      return;
    }
    const pn = pan.current;
    if (!pn) return;
    const dx = e.clientX - pn.sx, dy = e.clientY - pn.sy;
    if (!pn.moved && Math.hypot(dx, dy) < 6) return;
    if (!pn.moved) {
      pn.moved = true;
      movedRef.current = true;
      ref.current?.setPointerCapture(e.pointerId);
    }
    setOffset({ x: pn.ox + dx, y: pn.oy + dy });
  };
  const onUp = () => { setDrag(null); pan.current = null; };

  return (
    <div className="space-y-2">
      <div
        ref={ref}
        onPointerDown={onBgDown}
        onPointerMove={onMove}
        onPointerUp={onUp}
        onPointerCancel={onUp}
        className="relative w-full overflow-hidden rounded-lg border border-border bg-card shadow-elevated touch-none select-none cursor-grab active:cursor-grabbing"
        style={{ height: containerH }}
      >
        <div
          className="absolute left-0 top-0"
          style={{
            width: mapSize.width,
            height: mapSize.height,
            transformOrigin: "0 0",
            transform: `translate(${offset.x}px, ${offset.y}px) scale(${scale})`,
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
                onPointerDown={(e) => onStandDown(e, s)}
                onClick={() => {
                  if (movedRef.current) return;
                  if (editable) onSelect?.(s);
                  else if (["food", "drink", "beer", "dessert"].includes(s.category))
                    navigate({ to: "/events/$eventId/stands/$standId", params: { eventId, standId: s.id } });
                }}
                className={`absolute flex flex-col items-center justify-center gap-1 rounded-xl border-2 transition-colors ${
                  editable && mode === "move" ? "cursor-move" : "cursor-pointer"
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

        <div className="absolute right-2 top-2 flex flex-col gap-1" onPointerDown={(e) => e.stopPropagation()}>
          <Button size="icon" variant="secondary" className="h-9 w-9" aria-label="Aproximar" onClick={() => zoomCenter(1.25)}><ZoomIn className="w-4 h-4" /></Button>
          <Button size="icon" variant="secondary" className="h-9 w-9" aria-label="Afastar" onClick={() => zoomCenter(0.8)}><ZoomOut className="w-4 h-4" /></Button>
          <Button size="icon" variant="secondary" className="h-9 w-9" aria-label="Ver mapa inteiro" onClick={reset}><Maximize2 className="w-4 h-4" /></Button>
        </div>
        <div className="absolute left-2 bottom-2 rounded bg-background/80 px-2 py-0.5 text-[11px] text-muted-foreground pointer-events-none">
          {Math.round(zoom * 100)}%
        </div>
      </div>

      {editable && (
        <div className="flex gap-2">
          <Button size="sm" variant={mode === "move" ? "default" : "outline"} onClick={() => setMode("move")}><Move className="w-4 h-4 mr-1" /> Mover barracas</Button>
          <Button size="sm" variant={mode === "pan" ? "default" : "outline"} onClick={() => setMode("pan")}><Hand className="w-4 h-4 mr-1" /> Mover mapa</Button>
        </div>
      )}
    </div>
  );
}
