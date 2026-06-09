import { Component, type ReactNode } from "react";

interface State { error: Error | null }

export class ErrorBoundary extends Component<{ children: ReactNode }, State> {
  state: State = { error: null };

  static getDerivedStateFromError(error: Error): State {
    return { error };
  }

  componentDidCatch(error: Error, info: { componentStack?: string }) {
    // Detailed logging for fast diagnosis
    console.group("%c[EventFlow ErrorBoundary]", "color:#e94560;font-weight:bold");
    console.error("Error:", error);
    console.error("Message:", error.message);
    console.error("Stack:", error.stack);
    if (info?.componentStack) console.error("Component stack:", info.componentStack);
    console.groupEnd();
  }

  reset = () => this.setState({ error: null });

  render() {
    if (this.state.error) {
      return (
        <div className="flex min-h-screen items-center justify-center bg-background px-4">
          <div className="max-w-lg w-full text-center rounded-2xl border border-destructive/40 bg-card p-8">
            <h1 className="text-2xl font-bold text-destructive mb-2">Erro inesperado</h1>
            <p className="text-sm text-muted-foreground mb-4">{this.state.error.message}</p>
            <details className="text-left text-xs text-muted-foreground bg-secondary/50 rounded-lg p-3 mb-4 max-h-48 overflow-auto">
              <summary className="cursor-pointer font-medium mb-1">Stack trace</summary>
              <pre className="whitespace-pre-wrap break-words">{this.state.error.stack}</pre>
            </details>
            <div className="flex justify-center gap-2">
              <button onClick={this.reset} className="px-4 py-2 rounded-md gradient-hero text-primary-foreground text-sm font-medium">
                Tentar novamente
              </button>
              <a href="/" className="px-4 py-2 rounded-md bg-secondary text-foreground text-sm font-medium">Início</a>
            </div>
          </div>
        </div>
      );
    }
    return this.props.children;
  }
}
