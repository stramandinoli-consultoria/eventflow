import { createFileRoute, useNavigate, Link } from "@tanstack/react-router";
import { useState } from "react";
import { useStore } from "@/lib/store";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { toast } from "sonner";
import { UtensilsCrossed } from "lucide-react";

export const Route = createFileRoute("/auth")({ component: AuthPage });

function AuthPage() {
  const navigate = useNavigate();
  const login = useStore((s) => s.login);
  const register = useStore((s) => s.register);
  const [mode, setMode] = useState<"login" | "register">("login");
  const [form, setForm] = useState({ name: "", email: "", password: "", phone: "" });

  const redirectFor = (role: string, partnerId?: string) => {
    if (role === "admin") navigate({ to: "/admin" });
    else if (role === "partner") navigate({ to: "/partner" });
    else navigate({ to: "/me" });
  };

  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    if (mode === "login") {
      const u = login(form.email, form.password);
      if (!u) return toast.error("Credenciais inválidas");
      toast.success(`Bem-vindo(a), ${u.name}!`);
      redirectFor(u.role, u.partnerId);
    } else {
      if (!form.name || !form.email || !form.password) return toast.error("Preencha todos os campos");
      const u = register({ ...form, role: "customer" });
      toast.success(`Conta criada, ${u.name}!`);
      redirectFor(u.role);
    }
  };

  return (
    <div className="min-h-[calc(100vh-4rem)] grid place-items-center px-6 py-12">
      <div className="w-full max-w-md rounded-3xl border border-border gradient-card p-8 shadow-elevated">
        <div className="flex items-center gap-2 mb-6">
          <div className="w-10 h-10 rounded-xl gradient-hero grid place-items-center shadow-glow">
            <UtensilsCrossed className="w-5 h-5 text-primary-foreground" />
          </div>
          <span className="font-bold text-lg">EventFlow</span>
        </div>
        <h1 className="text-2xl font-bold mb-1">{mode === "login" ? "Entrar" : "Criar conta"}</h1>
        <p className="text-sm text-muted-foreground mb-6">
          {mode === "login" ? "Acesse sua conta para fazer pedidos." : "Cadastre-se como cliente em segundos."}
        </p>

        <form onSubmit={submit} className="space-y-4">
          {mode === "register" && (
            <div><Label>Nome</Label><Input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} /></div>
          )}
          <div><Label>E-mail</Label><Input type="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} /></div>
          <div><Label>Senha</Label><Input type="password" value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} /></div>
          {mode === "register" && (
            <div><Label>WhatsApp</Label><Input value={form.phone} onChange={(e) => setForm({ ...form, phone: e.target.value })} placeholder="+55 11 99999-0000" /></div>
          )}
          <Button type="submit" className="w-full gradient-hero shadow-glow font-semibold">
            {mode === "login" ? "Entrar" : "Criar conta"}
          </Button>
        </form>

        <button onClick={() => setMode(mode === "login" ? "register" : "login")} className="block w-full text-center text-sm text-muted-foreground hover:text-foreground mt-5">
          {mode === "login" ? "Não tem conta? Cadastre-se" : "Já tem conta? Entrar"}
        </button>

        <div className="mt-6 p-3 rounded-lg bg-secondary/50 border border-border text-xs text-muted-foreground space-y-1">
          <p className="font-semibold text-foreground">Contas demo:</p>
          <p>admin@eventflow.app / admin123</p>
          <p>cliente@demo.com / demo123</p>
          <p>burger@demo.com / demo123 (parceiro)</p>
        </div>

        <Link to="/" className="block text-center text-xs text-muted-foreground hover:text-foreground mt-4">← Voltar</Link>
      </div>
    </div>
  );
}
