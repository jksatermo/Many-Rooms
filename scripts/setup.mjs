import { randomBytes } from "node:crypto";
import { readFile, writeFile } from "node:fs/promises";

const destination = new URL("../.env", import.meta.url);
const template = await readFile(
  new URL("../.env.example", import.meta.url),
  "utf8",
);
try {
  await writeFile(
    destination,
    template.replace(
      /^POSTGRES_PASSWORD=.*$/m,
      `POSTGRES_PASSWORD=${randomBytes(24).toString("base64url")}`,
    ),
    { flag: "wx", mode: 0o600 },
  );
  console.log(
    "Created ignored .env with a unique local database password. Next: npm run db:up",
  );
} catch (error) {
  if (error.code !== "EEXIST") throw error;
  console.log(".env already exists; kept its values. Next: npm run db:up");
}
