# Email Notification Setup (SMTP)

This guide enables real email delivery for notification events (collaboration updates, payment success/failure, renewal reminders, and plan expiry).

## 1. Enable Email Delivery

In `backend/InfluencerMatch.API/appsettings.json`, configure `EmailNotifications`:

```json
"EmailNotifications": {
  "Enabled": true,
  "FromEmail": "no-reply@yourdomain.com",
  "FromName": "InfluencerMatch",
  "SmtpHost": "smtp.your-provider.com",
  "SmtpPort": 587,
  "Username": "smtp-username",
  "Password": "smtp-password",
  "UseSsl": true
}
```

If `Enabled` is `false`, emails are not sent and the app falls back to structured logs.

## 2. Provider Presets

## SendGrid (SMTP)

Use when you already have a SendGrid account and verified sender/domain.

```json
"EmailNotifications": {
  "Enabled": true,
  "FromEmail": "no-reply@yourdomain.com",
  "FromName": "InfluencerMatch",
  "SmtpHost": "smtp.sendgrid.net",
  "SmtpPort": 587,
  "Username": "apikey",
  "Password": "<SENDGRID_API_KEY>",
  "UseSsl": true
}
```

## Amazon SES (SMTP)

Use SES SMTP credentials (not AWS access key/secret directly unless converted to SMTP credentials).

```json
"EmailNotifications": {
  "Enabled": true,
  "FromEmail": "no-reply@yourdomain.com",
  "FromName": "InfluencerMatch",
  "SmtpHost": "email-smtp.ap-south-1.amazonaws.com",
  "SmtpPort": 587,
  "Username": "<SES_SMTP_USERNAME>",
  "Password": "<SES_SMTP_PASSWORD>",
  "UseSsl": true
}
```

Region examples:
- `email-smtp.us-east-1.amazonaws.com`
- `email-smtp.eu-west-1.amazonaws.com`

## Mailgun (SMTP)

```json
"EmailNotifications": {
  "Enabled": true,
  "FromEmail": "no-reply@yourdomain.com",
  "FromName": "InfluencerMatch",
  "SmtpHost": "smtp.mailgun.org",
  "SmtpPort": 587,
  "Username": "postmaster@your-mailgun-domain",
  "Password": "<MAILGUN_SMTP_PASSWORD>",
  "UseSsl": true
}
```

## 3. Recommended Secret Handling

Do not commit real SMTP credentials into source control.

Use environment variables in production.

PowerShell examples:

```powershell
$env:EmailNotifications__Enabled="true"
$env:EmailNotifications__SmtpHost="smtp.sendgrid.net"
$env:EmailNotifications__SmtpPort="587"
$env:EmailNotifications__Username="apikey"
$env:EmailNotifications__Password="<SENDGRID_API_KEY>"
$env:EmailNotifications__FromEmail="no-reply@yourdomain.com"
$env:EmailNotifications__FromName="InfluencerMatch"
$env:EmailNotifications__UseSsl="true"
$env:App__FrontendBaseUrl="https://your-app.vercel.app"
```

`App__FrontendBaseUrl` is required for email verification and password reset links. If it is left at the local default, users will receive localhost links.

## 4. Quick Verification

1. Start API:

```bash
cd backend/InfluencerMatch.API
dotnet run
```

2. Trigger any notification path, for example:
- send/accept/reject a collaboration request
- successful/failed payment webhook
- subscription cancel/reactivate
- register a new account or request a password reset to verify auth email links

3. Check:
- in-app notifications (`GET /api/notifications`)
- API logs for email send success/failure
- recipient inbox/spam folder

## 5. Troubleshooting

- `Email enabled but SMTP configuration is incomplete`:
  Set `SmtpHost` and `FromEmail`.
- Auth failures (`535`/`530`):
  Verify SMTP username/password and sender/domain verification in provider console.
- TLS/connection errors:
  Confirm host/port and outbound network access from your runtime environment.
- No email but notification exists:
  In-app persistence succeeds independently; inspect API logs for SMTP send errors.
