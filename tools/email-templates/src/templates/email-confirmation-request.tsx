import type { EmailTemplateDefinition } from "../template-contract";
import { ActionLink, Paragraph, TemplateShell, TokenCode } from "./template-shell";

function EmailConfirmationRequest() {
  return (
    <TemplateShell preview="Confirm your email address" title="Confirm your email address">
      <Paragraph>Hi {"{{displayName}}"},</Paragraph>
      <Paragraph>
        Confirm your email address to finish creating your account. This link expires in 24 hours.
      </Paragraph>
      <ActionLink href="{{confirmationUrl}}">Confirm email address</ActionLink>
      <Paragraph>If the link does not work, copy this token into the confirmation screen:</Paragraph>
      <Paragraph>
        <TokenCode>{"{{token}}"}</TokenCode>
      </Paragraph>
      <Paragraph>If you did not create an account, ignore this email.</Paragraph>
    </TemplateShell>
  );
}

export const emailConfirmationRequestTemplate: EmailTemplateDefinition = {
  id: "users.email-confirmation-request",
  subject: "Confirm your email address",
  variables: {
    confirmationUrl: { type: "url", required: true },
    displayName: { type: "string", required: true },
    token: { type: "string", required: true },
  },
  render: EmailConfirmationRequest,
};
