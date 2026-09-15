import { AdminApi, ApiError } from "./api";

test("login posts to platform auth, not restaurant auth", async () => {
  const fetcher = vi.fn(async () => new Response(JSON.stringify({ accessToken: "t" }), { status: 200 }));
  const api = new AdminApi("https://api.test", fetcher as typeof fetch);
  await api.login("a@b.c", "password-12345");
  expect(fetcher).toHaveBeenCalledWith(
    expect.stringContaining("/api/v1/platform/auth/login"),
    expect.anything(),
  );
});

test("restaurant-style 403 surfaces as ApiError", async () => {
  const fetcher = vi.fn(
    async () =>
      new Response(JSON.stringify({ code: "PLATFORM_ACCESS_DENIED", detail: "denied" }), {
        status: 403,
        headers: { "Content-Type": "application/json" },
      }),
  );
  const api = new AdminApi("", fetcher as typeof fetch);
  await expect(api.login("owner@test", "x")).rejects.toBeInstanceOf(ApiError);
});
