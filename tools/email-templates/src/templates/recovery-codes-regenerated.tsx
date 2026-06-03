import type { EmailTemplateDefinition } from "../template-contract";
import { Paragraph, TemplateShell } from "./template-shell";

function RecoveryCodesRegenerated() {
  return (
    <TemplateShell preview="Recovery codes regenerated" title="Recovery codes regenerated">
      <Paragraph>
        Your two-factor recovery codes have been regenerated. If you did not make this change, reset
        your password and contact support immediately.
      </Paragraph>
    </TemplateShell>
  );
}

export const recoveryCodesRegeneratedTemplate: EmailTemplateDefinition = {
  id: "users.recovery-codes-regenerated",
  subject: "Recovery codes regenerated",
  variables: {},
  render: RecoveryCodesRegenerated,
};
