import type { EmailTemplateDefinition } from "../template-contract";
import { welcomeEmailTemplate } from "./welcome-email";

export const emailTemplates: EmailTemplateDefinition[] = [
  welcomeEmailTemplate,
];
