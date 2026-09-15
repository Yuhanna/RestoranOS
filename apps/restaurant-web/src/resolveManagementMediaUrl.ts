/** Resolves brand/media paths against the management API origin when it is not same-origin. */
export function resolveManagementMediaUrl(imageUrl: string, apiBaseUrl = ""): string {
  const trimmed = imageUrl.trim();
  if (!trimmed) {
    return "";
  }

  const base = apiBaseUrl.replace(/\/$/, "");

  try {
    const url = new URL(trimmed, base || "http://local.invalid");
    if (url.pathname.startsWith("/media")) {
      return base ? `${base}${url.pathname}${url.search}` : `${url.pathname}${url.search}`;
    }
    if (/^https?:\/\//i.test(trimmed)) {
      return trimmed;
    }
  } catch {
    return trimmed;
  }

  if (trimmed.startsWith("/")) {
    return base ? `${base}${trimmed}` : trimmed;
  }

  return trimmed;
}
