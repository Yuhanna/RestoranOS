/** Strips localhost / absolute API host from /media URLs so mobile Vite proxy can load images. */
export function resolveProductMediaUrl(imageUrl: string): string {
  if (!imageUrl) {
    return "";
  }

  const trimmed = imageUrl.trim();
  if (trimmed.startsWith("/media")) {
    return trimmed;
  }

  try {
    const url = new URL(trimmed, window.location.origin);
    if (url.pathname.startsWith("/media")) {
      return `${url.pathname}${url.search}`;
    }
  } catch {
    return trimmed;
  }

  return trimmed;
}
