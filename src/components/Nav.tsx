import { Link } from "@tanstack/react-router";
import { UtensilsCrossed, LayoutDashboard, Calendar } from "lucide-react";

export function Nav() {
  return (
    <header className="sticky top-0 z-50 backdrop-blur-xl bg-background/70 border-b border-border">
      <div className="max-w-7xl mx-auto px-6 h-16 flex items-center justify-between">
        <Link to="/" className="flex items-center gap-2 group">
          <div className="w-9 h-9 rounded-xl gradient-hero grid place-items-center shadow-glow group-hover:scale-105 transition-transform">
            <UtensilsCrossed className="w-5 h-5 text-primary-foreground" />
          </div>
          <span className="font-bold text-lg tracking-tight">EventFlow</span>
        </Link>
        <nav className="flex items-center gap-1">
          <Link
            to="/"
            className="px-3 py-2 rounded-lg text-sm font-medium text-muted-foreground hover:text-foreground hover:bg-secondary transition-colors flex items-center gap-2"
            activeOptions={{ exact: true }}
            activeProps={{ className: "text-foreground bg-secondary" }}
          >
            <Calendar className="w-4 h-4" /> Eventos
          </Link>
          <Link
            to="/admin"
            className="px-3 py-2 rounded-lg text-sm font-medium text-muted-foreground hover:text-foreground hover:bg-secondary transition-colors flex items-center gap-2"
            activeProps={{ className: "text-foreground bg-secondary" }}
          >
            <LayoutDashboard className="w-4 h-4" /> Admin
          </Link>
        </nav>
      </div>
    </header>
  );
}
