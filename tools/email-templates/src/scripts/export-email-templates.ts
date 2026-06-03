import { mkdir, writeFile } from "node:fs/promises";
import { join } from "node:path";
import { render } from "@react-email/render";
import { emailTemplates } from "../templates";
import type { TemplateManifest, TemplateSchema } from "../template-contract";
import { generatedTemplatesDir } from "./template-paths";
import { validateTemplateHtml } from "./template-validation";

const seenIds = new Set<string>();
const manifest: TemplateManifest = { templates: [] };
const errors: string[] = [];

await mkdir(generatedTemplatesDir, { recursive: true });

for (const template of emailTemplates) {
  if (seenIds.has(template.id)) {
    errors.push(`Duplicate template id '${template.id}'`);
    continue;
  }

  seenIds.add(template.id);

  const html = await render(template.render(), { pretty: true });
  const fileBaseName = template.id.replaceAll(".", "-");
  const htmlFile = `${fileBaseName}.html`;
  const schemaFile = `${fileBaseName}.schema.json`;
  const schema: TemplateSchema = {
    id: template.id,
    variables: template.variables,
  };

  errors.push(...validateTemplateHtml(schema, html));

  await writeFile(join(generatedTemplatesDir, htmlFile), `${html.trim()}\n`);
  await writeFile(join(generatedTemplatesDir, schemaFile), `${JSON.stringify(schema, null, 2)}\n`);

  manifest.templates.push({
    id: template.id,
    subject: template.subject,
    html: htmlFile,
    schema: schemaFile,
  });
}

manifest.templates.sort((left, right) => left.id.localeCompare(right.id));
await writeFile(
  join(generatedTemplatesDir, "templates.manifest.json"),
  `${JSON.stringify(manifest, null, 2)}\n`,
);

if (errors.length > 0) {
  console.error(errors.join("\n"));
  process.exitCode = 1;
}
