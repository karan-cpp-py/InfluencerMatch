# Monitoring and Alerts Setup

## Uptime checks
- Endpoint 1: `GET /health`
- Endpoint 2: `GET /readiness`
- Interval: 60 seconds
- Timeout: 10 seconds
- Regions: at least 2

## Alert rules
1. API downtime
- Condition: `/health` fails 2 consecutive checks
- Severity: High
- Action: page on-call + Slack alert

2. Database unavailable
- Condition: `/readiness` returns unhealthy/degraded for 2 checks
- Severity: Critical
- Action: page on-call + open incident

3. Payment failure spike
- Condition: failed payments per hour > `Monitoring:PaymentFailureAlertThresholdPerHour`
- Severity: High
- Action: alert engineering and temporarily disable affected provider if needed

## Suggested tools
- Uptime: UptimeRobot, Better Stack, Pingdom
- Error tracking: Azure Application Insights
- Log search: Azure Monitor / ELK

## Validation checklist
- Simulate DB outage and confirm readiness alert.
- Simulate payment provider outage and confirm degraded readiness.
- Confirm alert notifications reach primary and backup contacts.
