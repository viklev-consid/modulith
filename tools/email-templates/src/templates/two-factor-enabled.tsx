import type { EmailTemplateDefinition } from "../template-contract";
import { Paragraph, TemplateShell } from "./template-shell";

function TwoFactorEnabled() {
  return (
    <TemplateShell preview="Two-factor authentication enabled" title="Two-factor authentication enabled">
      <Paragraph>
        Two-factor authentication has been enabled for your account. If you did not make this
        change, reset your password and contact support immediately.
      </Paragraph>
    </TemplateShell>
  );
}

export const twoFactorEnabledTemplate: EmailTemplateDefinition = {
  id: "users.two-factor-enabled",
  subject: "Two-factor authentication enabled",
  variables: {},
  render: TwoFactorEnabled,
};
