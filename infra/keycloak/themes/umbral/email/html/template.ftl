<#--
  UMBRAL branded HTML email layout. Overrides the base `emailLayout` macro so every
  outgoing HTML email (onboarding, password reset, verify email…) is wrapped in a
  branded card. Styles are inline because email clients strip <style>/external CSS.
  Palette mirrors src/apps/mobile/src/theme/tokens.ts (bright Duolingo).
-->
<#macro emailLayout>
<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8"/>
  <meta name="viewport" content="width=device-width, initial-scale=1.0"/>
</head>
<body style="margin:0; padding:0; background:#f7f7f7; font-family:'Nunito', -apple-system, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color:#4b4b4b;">
  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f7f7f7; padding:24px 0;">
    <tr>
      <td align="center">
        <table role="presentation" width="480" cellpadding="0" cellspacing="0" style="width:480px; max-width:100%; background:#ffffff; border:1px solid #e5e5e5; border-radius:18px; overflow:hidden;">
          <tr>
            <td align="center" style="background:#58cc02; padding:22px 24px;">
              <span style="font-size:26px; font-weight:800; letter-spacing:2px; text-transform:uppercase; color:#ffffff;">UMBRAL</span>
            </td>
          </tr>
          <tr>
            <td style="padding:28px 32px; font-size:16px; line-height:1.6; color:#4b4b4b;">
              <#nested>
            </td>
          </tr>
          <tr>
            <td style="padding:16px 32px 24px; font-size:12px; line-height:1.5; color:#afafaf; border-top:1px solid #e5e5e5;">
              UMBRAL — misión inmersiva en tiempo real.
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>
</#macro>
