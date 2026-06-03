import type { ReactElement } from "react";

export type TemplateVariableType = "string" | "url";

export type TemplateVariable = {
  type: TemplateVariableType;
  required: boolean;
};

export type EmailTemplateDefinition = {
  id: string;
  subject: string;
  variables: Record<string, TemplateVariable>;
  render: () => ReactElement;
};

export type TemplateManifest = {
  templates: Array<{
    id: string;
    subject: string;
    html: string;
    schema: string;
  }>;
};

export type TemplateSchema = {
  id: string;
  variables: Record<string, TemplateVariable>;
};
