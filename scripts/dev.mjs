import { existsSync } from "node:fs";
import { spawn } from "node:child_process";
import { fileURLToPath } from "node:url";

const root = fileURLToPath(new URL("../", import.meta.url));
const envPath = fileURLToPath(new URL("../.env", import.meta.url));
if (!existsSync(envPath)) {
  console.error("Missing .env. Run npm run setup first.");
  process.exit(1);
}
process.loadEnvFile(envPath);
const env = { ...process.env };
const db = env.POSTGRES_DB || "manyrooms";
const user = env.POSTGRES_USER || "manyrooms";
if (!env.POSTGRES_PASSWORD) {
  console.error(
    "Set POSTGRES_PASSWORD in your ignored .env before starting PostgreSQL.",
  );
  process.exit(1);
}
// Quote connection values so punctuation in a local password cannot alter connection settings.
const quote = (value) => `"${String(value).replaceAll('"', '""')}"`;
const connection = (database) =>
  [
    `Host=${quote(env.POSTGRES_HOST || "127.0.0.1")}`,
    `Port=${quote(env.POSTGRES_PORT || "55432")}`,
    `Database=${quote(database)}`,
    `Username=${quote(user)}`,
    `Password=${quote(env.POSTGRES_PASSWORD)}`,
  ].join(";");
env.ConnectionStrings__ManyRooms ||= connection(db);
env.ASPNETCORE_ENVIRONMENT ||= "Development";
env.ASPNETCORE_URLS ||= "http://localhost:5080";
env.Demo__Enabled ||= "true";
let child;
for (const signal of ["SIGINT", "SIGTERM"]) {
  process.on(signal, () => child?.kill(signal));
}
async function run(command, args, capture = false) {
  return new Promise((resolve, reject) => {
    child = spawn(command, args, {
      cwd: root,
      env,
      stdio: capture ? ["ignore", "pipe", "inherit"] : "inherit",
      shell: false,
    });
    let output = "";
    child.stdout?.on("data", (data) => {
      output += data;
    });
    child.on("error", reject);
    child.on("exit", (code) =>
      code === 0
        ? resolve(output.trim())
        : reject(new Error(`${command} exited with status ${code}.`)),
    );
  });
}
const compose = ["compose", "--project-directory", root, "--env-file", envPath];
try {
  switch (process.argv[2]) {
    case "db-up":
      await run("docker", [...compose, "up", "-d", "--wait", "db"]);
      break;
    case "db-down":
      await run("docker", [...compose, "down"]);
      break;
    case "migrate":
      await run("dotnet", ["tool", "restore"]);
      await run("dotnet", [
        "ef",
        "database",
        "update",
        "--project",
        "src/ManyRooms.Infrastructure",
        "--startup-project",
        "src/ManyRooms.Api",
      ]);
      break;
    case "seed":
      await run("dotnet", [
        "run",
        "--project",
        "src/ManyRooms.Api",
        "--no-launch-profile",
        "--",
        "--seed",
      ]);
      break;
    case "api":
      await run("dotnet", [
        "run",
        "--project",
        "src/ManyRooms.Api",
        "--no-launch-profile",
      ]);
      break;
    case "test-backend": {
      // API tests use a dedicated local database; never point them at application data.
      const testDb = "manyrooms_test";
      const query = await run(
        "docker",
        [
          ...compose,
          "exec",
          "-T",
          "db",
          "psql",
          "-U",
          user,
          "-d",
          "postgres",
          "-tAc",
          `SELECT 1 FROM pg_database WHERE datname='${testDb}'`,
        ],
        true,
      );
      if (query !== "1")
        await run("docker", [
          ...compose,
          "exec",
          "-T",
          "db",
          "createdb",
          "-U",
          user,
          testDb,
        ]);
      env.TEST_DATABASE_CONNECTION = connection(testDb);
      await run("dotnet", [
        "test",
        "ManyRooms.sln",
        "--configuration",
        "Release",
      ]);
      break;
    }
    default:
      throw new Error(
        "Unknown development command. Use the scripts listed in package.json.",
      );
  }
} catch (error) {
  console.error(error.message);
  process.exitCode = 1;
}
