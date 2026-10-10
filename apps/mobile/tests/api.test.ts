import assert from "node:assert/strict";
import { test } from "node:test";
import { createApi } from "../src/lib/api.ts";

test("demo context is anonymous; organization requests explicitly identify the development demo", async () => {
  const requests: { url: string; headers: Headers }[] = [];
  const fetcher: typeof fetch = async (input, init) => {
    requests.push({ url: String(input), headers: new Headers(init?.headers) });
    return new Response(JSON.stringify({ organizationId: "cedar-grove" }), {
      status: 200,
    });
  };
  const api = createApi("http://localhost:5080/", fetcher);
  const context = await api.context();
  await api.dashboard(context.organizationId);
  assert.equal(requests[0].url, "http://localhost:5080/api/demo/context");
  assert.equal(requests[0].headers.has("X-ManyRooms-Demo"), false);
  assert.equal(
    requests[1].url,
    "http://localhost:5080/api/organizations/cedar-grove/dashboard",
  );
  assert.equal(requests[1].headers.get("X-ManyRooms-Demo"), "true");
});

test("disabled demo returns a helpful error rather than pretending to sign in", async () => {
  const api = createApi(
    "http://localhost:5080",
    async () => new Response(null, { status: 401 }),
  );
  await assert.rejects(
    api.dashboard("cedar-grove"),
    /development demo is unavailable/,
  );
});

test("server failure does not expose raw server details", async () => {
  const api = createApi(
    "http://localhost:5080",
    async () => new Response("internal details", { status: 500 }),
  );
  await assert.rejects(api.context(), /Check the API connection/);
});
