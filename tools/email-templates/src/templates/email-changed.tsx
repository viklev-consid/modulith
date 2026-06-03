import type { EmailTemplateDefinition } from "../template-contract";
import { Paragraph, Strong, TemplateShell } from "./template-shell";

function EmailChanged() {
  return (
    <TemplateShell preview="Your email address has been changed" title="Email address changed">
      <Paragraph>
        The email address on your account has been changed to <Strong>{"{{newEmail}}"}</Strong>.
      </Paragraph>
      <Paragraph>
        If you did not make this change, contact support immediately - your account may be
        compromised.
      </Paragraph>
    </TemplateShell>
  );
}

export const emailChangedTemplate: EmailTemplateDefinition = {
  id: "users.email-changed",
  subject: "Your email address has been changed",
  variables: {
    newEmail: { type: "string", required: true },
  },
  render: EmailChanged,
};
