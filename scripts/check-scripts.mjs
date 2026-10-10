import { readdir } from "node:fs/promises";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";
const directory = new URL("./", import.meta.url);
for (const file of await readdir(directory)) {
  if (!file.endsWith(".mjs")) continue;
  const result = spawnSync(
    process.execPath,
    ["--check", fileURLToPath(new URL(file, directory))],
    { stdio: "inherit" },
  );
  if (result.status !== 0) process.exit(result.status || 1);
}
console.log("Development scripts passed syntax checks.");
