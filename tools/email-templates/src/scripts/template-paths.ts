import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = dirname(fileURLToPath(import.meta.url));

export const projectRoot = resolve(scriptDir, "../../../..");
export const generatedTemplatesDir = resolve(
  projectRoot,
  "src/Modules/Notifications/Modulith.Modules.Notifications/Templates/Generated",
);
