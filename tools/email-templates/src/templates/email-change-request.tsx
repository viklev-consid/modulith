import type { EmailTemplateDefinition } from "../template-contract";
import { ActionLink, Paragraph, TemplateShell, TokenCode } from "./template-shell";

function EmailChangeRequest() {
  return (
    <TemplateShell preview="Confirm your email address change" title="Confirm email address change">
      <Paragraph>
        We received a request to change the email address on your account. Use the link below to
        confirm. It expires in 30 minutes.
      </Paragraph>
      <ActionLink href="{{confirmationUrl}}">Confirm email address change</ActionLink>
      <Paragraph>If the link does not work, copy this token into the confirmation screen:</Paragraph>
      <Paragraph>
        <TokenCode>{"{{token}}"}</TokenCode>
      </Paragraph>
      <Paragraph>
        If you did not make this request, you can safely ignore this email. Your email address will
        not change.
      </Paragraph>
    </TemplateShell>
  );
}

export const emailChangeRequestTemplate: EmailTemplateDefinition = {
  id: "users.email-change-request",
  subject: "Confirm your email address change",
  variables: {
    confirmationUrl: { type: "url", required: true },
    token: { type: "string", required: true },
  },
  render: EmailChangeRequest,
};
