import type { EmailTemplateDefinition } from "../template-contract";
import { Paragraph, TemplateShell } from "./template-shell";

function TwoFactorDisabled() {
  return (
    <TemplateShell preview="Two-factor authentication disabled" title="Two-factor authentication disabled">
      <Paragraph>
        Two-factor authentication has been disabled for your account. If you did not make this
        change, reset your password and contact support immediately.
      </Paragraph>
    </TemplateShell>
  );
}

export const twoFactorDisabledTemplate: EmailTemplateDefinition = {
  id: "users.two-factor-disabled",
  subject: "Two-factor authentication disabled",
  variables: {},
  render: TwoFactorDisabled,
};
