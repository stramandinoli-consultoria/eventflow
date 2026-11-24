import { createFileRoute, Outlet, redirect } from "@tanstack/react-router";
import { auth } from "@/lib/api";

export const Route = createFileRoute("/admin")({
  // A área administrativa exige login de admin (a API também confere em cada chamada).
  beforeLoad: () => {
    if (auth.currentUser()?.role !== "admin") throw redirect({ to: "/auth" });
  },
  component: AdminLayout,
});

function AdminLayout() {
  return <Outlet />;
}
