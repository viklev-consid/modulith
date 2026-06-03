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
import type { ReactNode } from "react";

type TemplateShellProps = {
  preview: string;
  title: string;
  children: ReactNode;
};

export function TemplateShell({ preview, title, children }: TemplateShellProps) {
  return (
    <Html>
      <Head />
      <Preview>{preview}</Preview>
      <Body style={main}>
        <Container style={container}>
          <Section>
            <Heading style={heading}>{title}</Heading>
            {children}
          </Section>
        </Container>
      </Body>
    </Html>
  );
}

export function Paragraph({ children }: { children: ReactNode }) {
  return <Text style={paragraph}>{children}</Text>;
}

export function TokenCode({ children }: { children: ReactNode }) {
  return <code style={code}>{children}</code>;
}

export function ActionLink({ href, children }: { href: string; children: ReactNode }) {
  return (
    <a href={href} style={button}>
      {children}
    </a>
  );
}

export function Strong({ children }: { children: ReactNode }) {
  return <strong style={strong}>{children}</strong>;
}

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
  margin: "0 0 16px",
};

const button = {
  backgroundColor: "#1f6feb",
  color: "#ffffff",
  display: "inline-block",
  fontSize: "16px",
  fontWeight: "600",
  lineHeight: "24px",
  margin: "4px 0 16px",
  padding: "10px 14px",
  textDecoration: "none",
};

const code = {
  backgroundColor: "#edf2f7",
  color: "#172033",
  display: "inline-block",
  fontFamily: '"SFMono-Regular", Consolas, "Liberation Mono", monospace',
  fontSize: "14px",
  lineHeight: "20px",
  padding: "4px 6px",
};

const strong = {
  color: "#172033",
  fontWeight: "700",
};
