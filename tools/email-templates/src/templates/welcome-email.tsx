import {
  Body,
  Container,
  Head,
  Heading,
  Html,
  Preview,
  Section,
  Text,
} from "@react-email/components";
import type { EmailTemplateDefinition } from "../template-contract";

function WelcomeEmail() {
  return (
    <Html>
      <Head />
      <Preview>Welcome to Modulith</Preview>
      <Body style={main}>
        <Container style={container}>
          <Section>
            <Heading style={heading}>Welcome, {"{{displayName}}"}!</Heading>
            <Text style={paragraph}>
              Your account has been created. You can now sign in and start using the platform.
            </Text>
          </Section>
        </Container>
      </Body>
    </Html>
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

const main = {
  backgroundColor: "#f6f8fb",
  color: "#172033",
  fontFamily:
    '-apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif',
};

const container = {
  backgroundColor: "#ffffff",
  border: "1px solid #d9e0ea",
  margin: "32px auto",
  padding: "32px",
  width: "560px",
};

const heading = {
  color: "#172033",
  fontSize: "28px",
  fontWeight: "700",
  lineHeight: "36px",
  margin: "0 0 16px",
};

const paragraph = {
  color: "#40516b",
  fontSize: "16px",
  lineHeight: "24px",
  margin: "0",
};
