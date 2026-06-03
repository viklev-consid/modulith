import type { EmailTemplateDefinition } from "../template-contract";
import { ActionLink, Paragraph, TemplateShell, TokenCode } from "./template-shell";

function PasswordResetRequest() {
  return (
    <TemplateShell preview="Reset your password" title="Password reset request">
      <Paragraph>
        We received a request to reset the password for your account. Use the link below to complete
        the reset. It expires in 30 minutes.
      </Paragraph>
      <ActionLink href="{{resetUrl}}">Reset your password</ActionLink>
      <Paragraph>If the link does not work, copy this token into the reset screen:</Paragraph>
      <Paragraph>
        <TokenCode>{"{{token}}"}</TokenCode>
      </Paragraph>
      <Paragraph>If you did not request a password reset, you can safely ignore this email.</Paragraph>
    </TemplateShell>
  );
}

export const passwordResetRequestTemplate: EmailTemplateDefinition = {
  id: "users.password-reset-request",
  subject: "Reset your password",
  variables: {
    resetUrl: { type: "url", required: true },
    token: { type: "string", required: true },
  },
  render: PasswordResetRequest,
};
