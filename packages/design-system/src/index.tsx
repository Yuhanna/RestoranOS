import {
  useEffect,
  useRef,
  type ButtonHTMLAttributes,
  type PropsWithChildren,
  type ReactNode,
} from "react";

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: "primary" | "secondary" | "ghost";
  fullWidth?: boolean;
};

export function Button({
  variant = "primary",
  fullWidth = false,
  className = "",
  type = "button",
  ...props
}: ButtonProps) {
  return (
    <button
      type={type}
      className={`ds-button ds-button--${variant}${fullWidth ? " ds-button--full" : ""} ${className}`.trim()}
      {...props}
    />
  );
}

type DialogProps = PropsWithChildren<{
  labelledBy: string;
  dismissLabel: string;
  onDismiss: () => void;
  footer?: ReactNode;
}>;

export function Dialog({ labelledBy, dismissLabel, onDismiss, children, footer }: DialogProps) {
  const dialogRef = useRef<HTMLElement>(null);

  useEffect(() => {
    const previousFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const dialog = dialogRef.current;
    const focusableSelector =
      'button:not([disabled]), input:not([disabled]), textarea:not([disabled]), select:not([disabled]), [href], [tabindex]:not([tabindex="-1"])';
    dialog?.querySelector<HTMLElement>(focusableSelector)?.focus();

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault();
        onDismiss();
        return;
      }
      if (event.key !== "Tab" || !dialog) return;
      const focusable = Array.from(dialog.querySelectorAll<HTMLElement>(focusableSelector));
      const first = focusable[0];
      const last = focusable.at(-1);
      if (!first || !last) return;
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    };

    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("keydown", handleKeyDown);
      previousFocus?.focus();
    };
  }, [onDismiss]);

  return (
    <div className="ds-dialog-layer">
      <button
        className="ds-dialog-scrim"
        tabIndex={-1}
        aria-label={dismissLabel}
        onClick={onDismiss}
      />
      <section
        ref={dialogRef}
        className="ds-dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby={labelledBy}
      >
        <div className="ds-dialog__body">{children}</div>
        {footer ? <footer className="ds-dialog__footer">{footer}</footer> : null}
      </section>
    </div>
  );
}
