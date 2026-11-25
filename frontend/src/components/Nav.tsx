import { Link, useNavigate } from "@tanstack/react-router";
import { UtensilsCrossed, LayoutDashboard, Calendar, LogIn, LogOut, User as UserIcon, Store } from "lucide-react";
import { useCurrentUser, useStore } from "@/lib/store";
import { Button } from "@/components/ui/button";

export function Nav() {
  const user = useCurrentUser();
  const logout = useStore((s) => s.logout);
  const navigate = useNavigate();

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
          <Link to="/" className="px-3 py-2 rounded-lg text-sm font-medium text-muted-foreground hover:text-foreground hover:bg-secondary flex items-center gap-2"
            activeOptions={{ exact: true }} activeProps={{ className: "text-foreground bg-secondary" }}>
            <Calendar className="w-4 h-4" /> Eventos
          </Link>

          {user?.role === "customer" && (
            <Link to="/me" className="px-3 py-2 rounded-lg text-sm font-medium text-muted-foreground hover:text-foreground hover:bg-secondary flex items-center gap-2"
              activeProps={{ className: "text-foreground bg-secondary" }}>
              <UserIcon className="w-4 h-4" /> Minha área
            </Link>
          )}
          {user?.role === "partner" && (
            <Link to="/partner" className="px-3 py-2 rounded-lg text-sm font-medium text-muted-foreground hover:text-foreground hover:bg-secondary flex items-center gap-2"
              activeProps={{ className: "text-foreground bg-secondary" }}>
              <Store className="w-4 h-4" /> Meu stand
            </Link>
          )}
          {user?.role === "admin" && (
            <Link to="/admin" className="px-3 py-2 rounded-lg text-sm font-medium text-muted-foreground hover:text-foreground hover:bg-secondary flex items-center gap-2"
              activeProps={{ className: "text-foreground bg-secondary" }}>
              <LayoutDashboard className="w-4 h-4" /> Admin
            </Link>
          )}

          {user ? (
            <Button variant="ghost" size="sm" onClick={() => { logout(); navigate({ to: "/" }); }} className="ml-2">
              <LogOut className="w-4 h-4 mr-1" /> Sair
            </Button>
          ) : (
            <Link to="/auth">
              <Button size="sm" className="gradient-hero ml-2"><LogIn className="w-4 h-4 mr-1" /> Entrar</Button>
            </Link>
          )}
        </nav>
      </div>
    </header>
  );
}
