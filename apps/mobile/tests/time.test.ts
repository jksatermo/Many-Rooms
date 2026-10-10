import assert from "node:assert/strict";
import { test } from "node:test";
import { localInput, localToUtc } from "../src/lib/time.ts";

test("availability input rejects invalid dates and nonexistent spring DST local times", () => {
  const previous = process.env.TZ;
  process.env.TZ = "America/Chicago";
  try {
    assert.throws(() => localToUtc("2027-02-30 09:00"), /valid local date/);
    assert.throws(() => localToUtc("2027-03-14 02:30"), /daylight saving/);
    assert.throws(() => localToUtc("tomorrow"), /YYYY-MM-DD/);
    assert.equal(localToUtc("2027-03-14 03:30"), "2027-03-14T08:30:00.000Z");
  } finally {
    if (previous === undefined) delete process.env.TZ;
    else process.env.TZ = previous;
  }
});

test("repeated autumn hour uses the earlier occurrence and normal saved dates round-trip", () => {
  const previous = process.env.TZ;
  process.env.TZ = "America/Chicago";
  try {
    assert.equal(localToUtc("2027-11-07 01:30"), "2027-11-07T06:30:00.000Z");
    assert.equal(localInput("2027-04-18T14:30:00Z"), "2027-04-18 09:30");
    assert.equal(
      localToUtc(localInput("2027-04-18T14:30:00Z")),
      "2027-04-18T14:30:00.000Z",
    );
  } finally {
    if (previous === undefined) delete process.env.TZ;
    else process.env.TZ = previous;
  }
});
