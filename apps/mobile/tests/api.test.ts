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

test("Accept and Decline persist through the shared endpoint and a new adapter retrieves the latest version", async () => {
  let saved = {
    id: "assignment",
    status: "Pending",
    version: 2,
    conflicts: [],
  };
  const requests: { url: string; method: string; body: unknown }[] = [];
  const fetcher: typeof fetch = async (input, init) => {
    requests.push({
      url: String(input),
      method: init?.method ?? "GET",
      body: init?.body && JSON.parse(String(init.body)),
    });
    if (init?.method === "PUT") {
      assert.equal(new Headers(init.headers).get("X-ManyRooms-Demo"), "true");
      assert.equal(
        new Headers(init.headers).get("Content-Type"),
        "application/json",
      );
      const body = JSON.parse(String(init.body));
      assert.equal(body.expectedVersion, saved.version);
      saved = { ...saved, status: body.decision, version: saved.version + 1 };
      return Response.json(saved);
    }
    return Response.json({ assignments: [saved] });
  };
  const api = createApi("http://localhost:5080", fetcher);
  const accepted = await api.respond("cedar", "assignment", "Accepted", 2);
  assert.equal(accepted.status, "Accepted");
  const declined = await api.respond(
    "cedar",
    "assignment",
    "Declined",
    accepted.version,
  );
  assert.equal(declined.status, "Declined");
  const restarted = createApi("http://localhost:5080", fetcher);
  const event = await restarted.event("cedar", "gathering");
  assert.equal(event.assignments[0].status, "Declined");
  assert.equal(event.assignments[0].version, 4);
  assert.equal(
    requests[0].url,
    "http://localhost:5080/api/organizations/cedar/assignments/assignment/response",
  );
  assert.equal(requests[1].method, "PUT");
  assert.deepEqual(requests[1].body, {
    decision: "Declined",
    expectedVersion: 3,
  });
  assert.equal(requests[2].method, "GET");
});

test("stale response returns a typed conflict and never automatically retries a write", async () => {
  let count = 0;
  const api = createApi("http://localhost:5080", async () => {
    count++;
    return Response.json({}, { status: 409 });
  });
  await assert.rejects(
    api.respond("cedar", "assignment", "Accepted", 1),
    (error: unknown) =>
      error instanceof Error && "status" in error && error.status === 409,
  );
  assert.equal(count, 1);
});

test("availability mutations carry optimistic versions through update and delete", async () => {
  const requests: { url: string; init?: RequestInit }[] = [];
  const api = createApi("http://localhost:5080", async (input, init) => {
    requests.push({ url: String(input), init });
    return init?.method === "DELETE"
      ? new Response(null, { status: 204 })
      : Response.json({ id: "away", version: 2 });
  });
  const input = {
    startsAt: "2099-04-18T09:00:00Z",
    endsAt: "2099-04-18T10:00:00Z",
    note: null,
  };
  await api.createAvailability("cedar", input);
  await api.updateAvailability("cedar", "away", input, 2);
  await api.deleteAvailability("cedar", "away", 3);
  assert.equal(requests[0].init?.method, "POST");
  assert.deepEqual(JSON.parse(String(requests[1].init?.body)), {
    ...input,
    expectedVersion: 2,
  });
  assert.equal(
    requests[2].url,
    "http://localhost:5080/api/organizations/cedar/availability/away?expectedVersion=3",
  );
  assert.equal(requests[2].init?.method, "DELETE");
});
