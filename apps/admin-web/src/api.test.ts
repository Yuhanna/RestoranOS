import { AdminApi, ApiError } from "./api";

test("login posts to platform auth, not restaurant auth", async () => {
  const fetcher = vi.fn(async () => new Response(JSON.stringify({ accessToken: "t" }), { status: 200 }));
  const api = new AdminApi("https://api.test", fetcher);
  await api.login("a@b.c", "password-12345");
  expect(String(fetcher.mock.calls[0]?.[0])).toContain("/api/v1/platform/auth/login");
});

test("restaurant-style 403 surfaces as ApiError", async () => {
  const fetcher = vi.fn(
    async () =>
      new Response(JSON.stringify({ code: "PLATFORM_ACCESS_DENIED", detail: "denied" }), {
        status: 403,
        headers: { "Content-Type": "application/json" },
      }),
  );
  const api = new AdminApi("", fetcher);
  await expect(api.login("owner@test", "x")).rejects.toBeInstanceOf(ApiError);
});
