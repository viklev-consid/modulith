import type { EmailTemplateDefinition } from "../template-contract";
import { Paragraph, TemplateShell } from "./template-shell";

function PasswordResetConfirmation() {
  return (
    <TemplateShell preview="Your password has been reset" title="Password reset successful">
      <Paragraph>
        Your password has been reset. If you did not make this change, contact support immediately.
      </Paragraph>
    </TemplateShell>
  );
}

export const passwordResetConfirmationTemplate: EmailTemplateDefinition = {
  id: "users.password-reset-confirmation",
  subject: "Your password has been reset",
  variables: {},
  render: PasswordResetConfirmation,
};
