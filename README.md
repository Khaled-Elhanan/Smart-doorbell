<div align="center">

# 🔔 Smart Doorbell

**An event-driven backend that keeps working when devices, networks and servers don't.**

![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)
![MQTT](https://img.shields.io/badge/MQTT-5-660066?logo=mqtt&logoColor=white)
![SQL Server](https://img.shields.io/badge/SQL%20Server-CC2927?logo=microsoftsqlserver&logoColor=white)
![SignalR](https://img.shields.io/badge/SignalR-realtime-512BD4)
![Firebase](https://img.shields.io/badge/Firebase-FCM-FFCA28?logo=firebase&logoColor=black)
![Architecture](https://img.shields.io/badge/Clean%20Architecture-CQRS-2ea44f)

</div>

---

## What it does

Someone presses the doorbell → the backend takes a **snapshot** from the camera, triggers the **chime**, and **notifies the owner** (live via SignalR, or push via FCM if offline). The owner can then **acknowledge** or **dismiss** the ring.

Every step is **idempotent**, **crash-safe**, and handles **silent devices**, **duplicate messages**, and **concurrent workers**.

---

## See it run

The built-in demo page runs the **real pipeline** end-to-end: real MQTT broker, real consumers, real orchestration, real signed upload. Only the camera and chime are simulated.

<div align="center">
<img src="https://raw.githubusercontent.com/Khaled-Elhanan/Smart-doorbell/86118dc/docs/demo-dashboard.png" alt="Demo dashboard — snapshot captured, chime played, notification delivered via SignalR" width="900">
</div>

<br>

<div align="center">
<img src="https://raw.githubusercontent.com/Khaled-Elhanan/Smart-doorbell/86118dc/docs/demo-timeline.png" alt="Live timeline showing all pipeline stages completed" width="900">
</div>

**What the demo shows:**

- **Ring Doorbell** → publishes a real MQTT message (QoS 1)
- **Snapshot: Captured** → camera uploaded photo via signed URL, resolution shown
- **Chime: Played** → chime device acknowledged
- **Outbox: Delivered** → notification sent, delivery channel and attempt count shown
- **Live Timeline** → every stage with timestamps and durations
- **Acknowledge / Dismiss** → JWT-protected, updates ring status
- **SignalR ↔ FCM toggle** → switch delivery channel and watch the fallback

---

## Pipeline flow

```mermaid
flowchart TD
    BTN(["🔘 Button pressed"]) --> RING["Ring published to MQTT, QoS 1"]

    subgraph S1["1 · Ingest and guard"]
        RING --> VALID{"Valid JSON and deviceId matches topic?"}
        VALID -->|no| DLQ["Dead-letter to topic/dlq, then ACK"]
        VALID -->|yes| DUP{"DeviceEventId seen before?"}
        DUP -->|yes| SKIP["Return existing event, ACK"]
    end

    subgraph S2["2 · Record the intent first"]
        DUP -->|no| EVT[("Save ring event")]
        EVT --> INTENT[("Save snapshot + chime commands and outbox row in ONE transaction")]
    end

    subgraph S3["3 · Device round trip"]
        INTENT --> SNAPCMD["Publish snapshot command with signed upload URL"]
        INTENT --> CHIMECMD["Publish chime command"]
        SNAPCMD --> UP["Camera uploads photo, publishes result"]
        CHIMECMD --> PLAY["Chime plays, publishes ack"]
        UP --> CAP["Snapshot = Captured"]
        PLAY --> PLAYED["Chime = Played"]
    end

    subgraph S4["4 · Safety nets"]
        SNAPCMD -.->|no reply| TO1["Reconciler: Snapshot = TimedOut"]
        CHIMECMD -.->|no ack| TO2["Reconciler: Chime = Unknown"]
    end

    subgraph S5["5 · Notify and acknowledge"]
        INTENT --> JOB["Outbox job picks up the row"]
        JOB --> ONLINE{"Owner connected?"}
        ONLINE -->|yes| SIG["SignalR"]
        ONLINE -->|no| FCM["FCM push"]
        SIG -->|fails, fallback enabled| FCM
        SIG --> SENT["Outbox = Sent"]
        FCM --> SENT
        FCM -->|fails| RETRY["Retry until MaxAttempts"]
        RETRY --> JOB
        SENT --> ACK["Owner acknowledges, JWT"]
        ACK --> DONE(["Ring = Acknowledged"])
    end

    classDef store fill:#e7f0fd,stroke:#3b82f6,color:#111
    classDef bad fill:#fdecea,stroke:#ef4444,color:#111
    classDef good fill:#e8f7ee,stroke:#22c55e,color:#111
    class EVT,INTENT store
    class DLQ,TO1,TO2,RETRY bad
    class CAP,PLAYED,SENT,DONE good
```

1. **Ingest & guard** — bad payloads → dead-letter topic; duplicates → safe dedup.
2. **Record intent** — commands + outbox saved in one transaction *before* publishing.
3. **Device round trip** — camera uploads to a signed URL (photo never travels through MQTT).
4. **Safety nets** — silent camera → `TimedOut`; silent chime → `Unknown` (not `Failed`).
5. **Notify & acknowledge** — SignalR → FCM fallback, with retry and full attempt history.

---

## Failure handling

| Scenario | Response |
|---|---|
| Same ring delivered twice | One event, one snapshot, one chime (idempotent) |
| Server crashes after save | Intent in DB + deterministic IDs → replays are safe |
| Garbage payload | Dead-lettered and ACKed |
| Camera never responds | Snapshot → `TimedOut` |
| Chime never acks | Chime → `Unknown` (silence ≠ failure) |
| Two workers grab one notification | `RowVersion` optimistic concurrency → one wins |
| Worker dies mid-send | Stuck `Processing` rows recovered after timeout |
| Owner offline | SignalR → FCM fallback |
| MQTT broker drops | Reconnect with exponential backoff + jitter |
| Upload URL leaks | HMAC-SHA256, scoped, expires in 5 min |

---

## Architecture

**Clean Architecture + CQRS (MediatR)** — `Api → Infrastructure → Application → Domain`

Handlers depend only on interfaces, so MQTT, storage, or push providers can be swapped without touching business logic. Entities enforce their own state machines:

```text
Snapshot     NotRequested → Requested → Captured | TimedOut | Failed
Chime        NotRequested → Requested → Played   | Failed   | Unknown
Acknowledge  Pending → Acknowledged | Missed
Command      Pending → Sent → Completed | Failed → (Retry) → Pending
Outbox       Pending → Processing → Sent | Failed   (up to MaxAttempts)
```

**Stack:** .NET 10 · ASP.NET Core · MQTTnet 5 · EF Core 10 · SQL Server · MediatR · SignalR · Firebase Admin SDK · JWT · xUnit · Moq (70+ tests)

---

## Getting started

**Prerequisites:** .NET 10 SDK · SQL Server · MQTT 5 broker on `localhost:1883` (e.g. Mosquitto) · `dotnet-ef`

```bash
git clone https://github.com/Khaled-Elhanan/smart-doorbell.git && cd smart-doorbell

# Set the connection string
cd src/Doorbell.Api
dotnet user-secrets set "ConnectionStrings:MyConnection" \
  "Server=localhost,1433;Database=DoorbellDb;User Id=sa;Password=<pwd>;TrustServerCertificate=True"
cd ../..

# Create database and run
dotnet ef database update --project src/Doorbell.Infrastructure --startup-project src/Doorbell.Api
dotnet run --project src/Doorbell.Api --launch-profile https
```

Open **https://localhost:7101/demo/** and press **Ring Doorbell**.
Swagger is at **http://localhost:5260** (Development only).

---

<div align="center">

**Khaled Elhanan** · [GitHub](https://github.com/Khaled-Elhanan) · [khaledabdelhanansaad@gmail.com](mailto:khaledabdelhanansaad@gmail.com)

</div>
