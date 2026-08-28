import { Component, type ErrorInfo, type ReactNode } from "react";
import { Button } from "@restaurant-os/design-system";
import { messages as t } from "../i18n/messages";

type Props = { children: ReactNode };
type State = { error: Error | null };

export class AppErrorBoundary extends Component<Props, State> {
  state: State = { error: null };

  static getDerivedStateFromError(error: Error): State {
    return { error };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error("Customer web crashed", error, info.componentStack);
  }

  render() {
    if (!this.state.error) return this.props.children;

    return (
      <main className="entry-state" id="main-content">
        <span className="entry-state__mark entry-state__mark--error" aria-hidden="true">
          !
        </span>
        <h1>{t.qrLoadErrorTitle}</h1>
        <p>{t.qrLoadErrorBody}</p>
        <p className="service-note">{this.state.error.message}</p>
        <Button onClick={() => window.location.reload()}>{t.retry}</Button>
      </main>
    );
  }
}
