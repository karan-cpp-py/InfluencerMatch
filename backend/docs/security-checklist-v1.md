# Security Checklist v1 (Private Beta Sign-off)

Date: 2026-03-07
Scope: Influencer marketplace API and billing platform

## 1) Webhook verification
- [x] Stripe webhook signature verification implemented (`Stripe-Signature` with `t=` + `v1=` HMAC check).
- [x] Razorpay webhook signature verification implemented (`X-Razorpay-Signature` HMAC check).
- [x] PayPal webhook signature verification implemented via `/v1/notifications/verify-webhook-signature`.
- [x] Unsigned webhook requests rejected with `401`.
- [x] Replay detection enabled via `WebhookEvents` unique provider/event replay keys.
- [x] Verification failures logged with provider and reason.

## 2) Rate limiting
- [x] Auth endpoints rate-limited (`authPolicy`: 10 requests/minute partitioned by IP).
- [x] Webhook endpoints rate-limited (`webhookPolicy`: 30 requests/minute partitioned by IP).
- [x] Subscription/payment operations rate-limited (`paymentsPolicy`: 20 requests/minute).

## 3) Token security
- [x] Access token includes `token_use=access` claim.
- [x] Admin access token expiry reduced to 15 minutes.
- [x] Refresh token issued and persisted.
- [x] Refresh token rotation implemented.
- [x] Refresh token revocation endpoint implemented.

## 4) Admin access control
- [x] Admin middleware enforces `/api/admin/*` requests to authenticated `SuperAdmin` role.
- [x] Admin middleware requires access tokens only (`token_use=access`).

## 5) Password hashing
- [x] Password hashing uses BCrypt (`BCrypt.Net`) for register and seeded admin creation.

## 6) Remaining security recommendations before public launch
- [ ] Move all secrets (DB/JWT/API keys) to secure secret manager (Azure Key Vault / AWS Secrets Manager).
- [ ] Enable MFA for SuperAdmin login.
- [ ] Add WAF and bot detection in front of API.
- [ ] Add automated dependency vulnerability scanning in CI.

Sign-off:
- Status: APPROVED FOR PRIVATE BETA
- Owner: Backend Security Track
