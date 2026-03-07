# Incident Runbook (1-page)

## Detect
- Monitor `/health` and `/readiness` endpoints every 60s.
- Trigger alerts on:
- API downtime > 2 minutes
- Readiness failures for database check
- Payment failure spike above configured threshold
- Use correlation IDs (`X-Correlation-Id`) in logs for traceability.

## Triage
1. Confirm severity:
- P1: full outage, auth/billing unavailable
- P2: partial outage or major payment degradation
- P3: degraded analytics/background jobs
2. Identify impacted components:
- API
- Database
- Payment provider integration
- Background jobs

## Mitigate
1. Disable broken features safely:
- Temporarily disable provider by config flag in `PaymentProviders.*.Enabled`.
- Disable expensive background jobs via admin panel.
2. Route traffic to last stable deployment if current release is unstable.
3. For payment incident:
- Pause subscription upgrades/checkout endpoint if duplicate/incorrect charging observed.

## Rollback
1. Roll back to previous tagged deployment image.
2. Reapply previous appsettings from secure config store.
3. Validate with smoke checks:
- `/health`
- `/readiness`
- `/api/auth/login`
- `/api/subscriptions/current`

## Restore from backup
1. Select latest healthy daily backup from backup folder/storage.
2. Restore into staging first and verify row counts + critical tables.
3. Restore production database only after validation.
4. Run post-restore checks:
- users/subscriptions/payments counts
- latest invoices
- webhook event integrity

## Communicate
1. Open incident channel and assign owner.
2. Send status updates every 15 minutes for P1/P2.
3. Publish post-incident summary with root cause and corrective actions.

## Post-incident actions
- Add regression tests for failure mode.
- Update alert thresholds or dashboards.
- Update this runbook if process changed.
