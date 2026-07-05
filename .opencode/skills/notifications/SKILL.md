---
name: notifications
description: 'Notification patterns: in-app, email, push, webhook notifications with
  templates and delivery tracking. Trigger: When implementing notifications, alerts,
  or messaging systems.'
metadata:
  phase:
  - construction
  layer:
  - backend
  - frontend
  enforcement: recommended
  depends_on:
  - backend-api
  consumed_by:
  - agent-fullstack
  - agent-backend
  agent_roles:
  - design-agent
  - delivery-agent
  validation_profile: architecture-consistency
---

## Critical Rules
| Rule | Type | Rationale |
|------|------|-----------|
| Use async/event-driven delivery | ALWAYS | Avoid blocking on notification send |
| Persist notification state for retry | ALWAYS | Reliability |
| Support opt-out/unsubscribe | ALWAYS | Legal/compliance |
| Use templates for content | ALWAYS | Consistency and maintainability |
| Never include sensitive data in notification body | NEVER | Security risk |
| Log notification outcome (sent, failed, bounced) | ALWAYS | Observability |

## Notification Types

| Type | Channel | Use case |
|------|---------|---------|
| In-app | WebSocket / polling / SSE | Real-time alerts |
| Email | SMTP / SES / SendGrid | Confirmations, digests |
| SMS | Twilio / Vonage | Critical alerts |
| Push | FCM / APNs / OneSignal | Mobile apps |
| Webhook | HTTP POST | System-to-system events |
| Slack/Teams | Incoming webhooks | Team alerts |

## Notification Database Schema
```sql
CREATE TABLE Notifications.NotificationMessage (
    NotificationMessageId   INT PRIMARY KEY IDENTITY,
    UserId                  NVARCHAR(128) NOT NULL,
    Channel                 NVARCHAR(50)  NOT NULL,  -- 'email', 'in-app', 'push'
    TemplateId              NVARCHAR(100) NOT NULL,
    Subject                 NVARCHAR(500) NULL,
    Body                    NVARCHAR(MAX) NOT NULL,
    Status                  NVARCHAR(50)  NOT NULL DEFAULT 'PENDING',  -- PENDING/SENT/FAILED
    SentAt                  DATETIME2     NULL,
    RetryCount              INT           NOT NULL DEFAULT 0,
    MaxRetries              INT           NOT NULL DEFAULT 3,
    RecordCreationDate      DATETIME2     NOT NULL DEFAULT GETUTCDATE()
);
```

## Notification Status Machine
```
PENDING → SENDING → SENT
PENDING → SENDING → FAILED → RETRY → SENT / PERMANENTLY_FAILED
```

## Event-Driven Delivery (Preferred)
```
Domain Event (OrderCreated) → Event Bus / Queue →
Notification Consumer → Resolve template → Deliver to channel → Update status
```

## Template Pattern
```typescript
interface NotificationTemplate {
  templateId: string;
  channel: 'email' | 'in-app' | 'push';
  subject: (vars: Record<string, string>) => string;
  body: (vars: Record<string, string>) => string;
}

const ORDER_CREATED: NotificationTemplate = {
  templateId: 'order.created',
  channel: 'email',
  subject: ({ orderNumber }) => `Order #${orderNumber} confirmed`,
  body: ({ orderNumber, userName }) =>
    `Hello ${userName}, your order #${orderNumber} has been confirmed.`,
};
```

## In-App Notification (Frontend — React)
```typescript
// Real-time via Server-Sent Events (SSE)
export function useNotifications() {
  const [notifications, setNotifications] = useState<Notification[]>([]);

  useEffect(() => {
    const sse = new EventSource('/api/notifications/stream');
    sse.onmessage = (event) => {
      const notification = JSON.parse(event.data);
      setNotifications(prev => [notification, ...prev]);
    };
    return () => sse.close();
  }, []);

  return { notifications };
}
```

## Provider Options

| Channel | Options |
|---------|---------|
| Email | AWS SES, SendGrid, Mailgun, Postmark |
| SMS | Twilio, Vonage, AWS SNS |
| Push | FCM (Android), APNs (iOS), OneSignal |
| Queuing | RabbitMQ, Azure Service Bus, AWS SQS, Redis Streams |

## Retry Policy
```csharp
// .NET Polly retry with exponential backoff
var retryPolicy = Policy.Handle<Exception>()
    .WaitAndRetryAsync(
        3,
        attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))
    );
```

## Delivery Tracking
| Metric | Description |
|--------|-------------|
| `sent_count` | Successfully delivered |
| `failed_count` | Failed after all retries |
| `open_rate` | % of emails opened (email only) |
| `bounce_rate` | Invalid addresses (email only) |
