import type { EmailTemplateDefinition } from "../template-contract";
import { Paragraph, TemplateShell } from "./template-shell";

function WelcomeEmail() {
  return (
    <TemplateShell preview="Welcome to Modulith" title={<>Welcome, {"{{displayName}}"}!</>}>
      <Paragraph>Your account has been created. You can now sign in and start using the platform.</Paragraph>
    </TemplateShell>
  );
}

export const welcomeEmailTemplate: EmailTemplateDefinition = {
  id: "users.welcome",
  subject: "Welcome to Modulith!",
  variables: {
    displayName: { type: "string", required: true },
  },
  render: WelcomeEmail,
};
