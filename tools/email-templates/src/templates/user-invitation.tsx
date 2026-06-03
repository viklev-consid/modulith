import type { EmailTemplateDefinition } from "../template-contract";
import { ActionLink, Paragraph, TemplateShell, TokenCode } from "./template-shell";

function UserInvitation() {
  return (
    <TemplateShell preview="You're invited to join" title="You're invited">
      <Paragraph>Use the link below to create your account and accept the invitation.</Paragraph>
      <ActionLink href="{{invitationUrl}}">Accept invitation</ActionLink>
      <Paragraph>If the link does not work, copy this token into the invitation screen:</Paragraph>
      <Paragraph>
        <TokenCode>{"{{token}}"}</TokenCode>
      </Paragraph>
      <Paragraph>If you did not expect this invitation, you can ignore this email.</Paragraph>
    </TemplateShell>
  );
}

export const userInvitationTemplate: EmailTemplateDefinition = {
  id: "users.invitation",
  subject: "You're invited to join",
  variables: {
    invitationUrl: { type: "url", required: true },
    token: { type: "string", required: true },
  },
  render: UserInvitation,
};
