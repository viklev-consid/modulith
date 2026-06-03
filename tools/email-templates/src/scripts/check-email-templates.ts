import { readFile } from "node:fs/promises";
import { join } from "node:path";
import type { TemplateManifest, TemplateSchema } from "../template-contract";
import { generatedTemplatesDir } from "./template-paths";
import { validateTemplateHtml } from "./template-validation";

const errors: string[] = [];
const manifestPath = join(generatedTemplatesDir, "templates.manifest.json");
const manifest = JSON.parse(await readFile(manifestPath, "utf8")) as TemplateManifest;
const seenIds = new Set<string>();

for (const entry of manifest.templates) {
  if (seenIds.has(entry.id)) {
    errors.push(`Duplicate template id '${entry.id}'`);
    continue;
  }

  seenIds.add(entry.id);

  try {
    const html = await readFile(join(generatedTemplatesDir, entry.html), "utf8");
    const schema = JSON.parse(
      await readFile(join(generatedTemplatesDir, entry.schema), "utf8"),
    ) as TemplateSchema;

    if (schema.id !== entry.id) {
      errors.push(`${entry.id}: schema id '${schema.id}' does not match manifest id`);
    }

    errors.push(...validateTemplateHtml(schema, html));
  } catch (error) {
    errors.push(`${entry.id}: ${(error as Error).message}`);
  }
}

if (errors.length > 0) {
  console.error(errors.join("\n"));
  process.exit(1);
}

console.log(`Validated ${manifest.templates.length} email template(s).`);
