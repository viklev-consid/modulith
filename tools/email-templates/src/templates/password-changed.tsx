import type { EmailTemplateDefinition } from "../template-contract";
import { Paragraph, TemplateShell } from "./template-shell";

function PasswordChanged() {
  return (
    <TemplateShell preview="Your password has been changed" title="Password changed">
      <Paragraph>
        Your account password has been changed. If you did not make this change, contact support
        immediately and consider resetting your password.
      </Paragraph>
    </TemplateShell>
  );
}

export const passwordChangedTemplate: EmailTemplateDefinition = {
  id: "users.password-changed",
  subject: "Your password has been changed",
  variables: {},
  render: PasswordChanged,
};
