import type { TemplateSchema } from "../template-contract";

const placeholderPattern = /\{\{\s*([a-z][a-zA-Z0-9]*)\s*\}\}/g;
const variableNamePattern = /^[a-z][a-zA-Z0-9]*$/;

export function extractPlaceholders(html: string): string[] {
  return [...html.matchAll(placeholderPattern)].map((match) => match[1]);
}

export function validateTemplateHtml(schema: TemplateSchema, html: string): string[] {
  const errors: string[] = [];
  const placeholders = new Set(extractPlaceholders(html));

  for (const variableName of Object.keys(schema.variables)) {
    if (!variableNamePattern.test(variableName)) {
      errors.push(`${schema.id}: variable '${variableName}' must be camelCase`);
    }
  }

  for (const placeholder of placeholders) {
    if (!schema.variables[placeholder]) {
      errors.push(`${schema.id}: placeholder '{{${placeholder}}}' is not declared in the schema`);
    }
  }

  for (const [name, variable] of Object.entries(schema.variables)) {
    if (variable.required && !placeholders.has(name)) {
      errors.push(`${schema.id}: required variable '${name}' is not used in the HTML`);
    }
  }

  return errors;
}
