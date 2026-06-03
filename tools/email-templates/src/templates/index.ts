import type { EmailTemplateDefinition } from "../template-contract";
import { emailChangedTemplate } from "./email-changed";
import { emailChangeRequestTemplate } from "./email-change-request";
import { emailConfirmationRequestTemplate } from "./email-confirmation-request";
import { organizationInvitationTemplate } from "./organization-invitation";
import { passwordChangedTemplate } from "./password-changed";
import { passwordResetConfirmationTemplate } from "./password-reset-confirmation";
import { passwordResetRequestTemplate } from "./password-reset-request";
import { recoveryCodesRegeneratedTemplate } from "./recovery-codes-regenerated";
import { twoFactorDisabledTemplate } from "./two-factor-disabled";
import { twoFactorEnabledTemplate } from "./two-factor-enabled";
import { userInvitationTemplate } from "./user-invitation";
import { welcomeEmailTemplate } from "./welcome-email";

export const emailTemplates: EmailTemplateDefinition[] = [
  emailChangeRequestTemplate,
  emailChangedTemplate,
  emailConfirmationRequestTemplate,
  organizationInvitationTemplate,
  passwordChangedTemplate,
  passwordResetConfirmationTemplate,
  passwordResetRequestTemplate,
  recoveryCodesRegeneratedTemplate,
  twoFactorDisabledTemplate,
  twoFactorEnabledTemplate,
  userInvitationTemplate,
  welcomeEmailTemplate,
];
