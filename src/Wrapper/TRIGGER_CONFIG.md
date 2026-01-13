# Streamer.bot Trigger Configuration

## Overview

Single action **"TankRequest Control"** with all triggers.
Each trigger sets an `action` argument, then runs the wrapper Execute Code.

---

## Setup

### 1. DLL Reference
- Copy `TankRequest.dll` to a folder
- In Execute Code → References → Add the DLL path

### 2. Create Action: "TankRequest Control"

---

## Triggers Configuration

### Credit Tokens (Twitch Subscription)

**Trigger:** `Twitch > Subscription`

**Sub-Actions:**
```
1. Set Argument: action = "credit_tokens"
2. Set Argument: eventSource = "Twitch"
3. Set Argument: eventType = "subscription"
4. Set Argument: tier = %tier%
5. Execute Code (wrapper)
```

### Credit Tokens (Twitch Cheer)

**Trigger:** `Twitch > Cheer`

**Sub-Actions:**
```
1. Set Argument: action = "credit_tokens"
2. Set Argument: eventSource = "Twitch"
3. Set Argument: eventType = "cheer"
4. Set Argument: bits = %bits%
5. Execute Code (wrapper)
```

### Credit Tokens (StreamElements Tip)

**Trigger:** `StreamElements > Tip`

**Sub-Actions:**
```
1. Set Argument: action = "credit_tokens"
2. Set Argument: eventSource = "StreamElements"
3. Set Argument: eventType = "tip"
4. Set Argument: tipAmount = %tipAmount%
5. Set Argument: tipCurrency = %tipCurrency%
6. Execute Code (wrapper)
```

### Supporter Redeem

**Trigger:** `Twitch > Channel Point Reward > [Supporter Tank]`

**Sub-Actions:**
```
1. Set Argument: action = "supporter_redeem"
2. Set Argument: rawInput = %rawInput%
3. Execute Code (wrapper)
4. if %allow% Equals "true" → [nothing needed, already fulfilled in code]
5. if %allow% Equals "false" → Twitch Redemption Cancel
```

### Normal Redeem

**Trigger:** `Twitch > Channel Point Reward > [Normal Tank]`

**Sub-Actions:**
```
1. Set Argument: action = "normal_redeem"
2. Set Argument: rawInput = %rawInput%
3. Execute Code (wrapper)
4. if %allow% Equals "true" → Twitch Send Message %displayMsg%
5. if %allow% Equals "false" → Twitch Redemption Cancel + Send Message %displayMsg%
```

### Dequeue (Hotkey)

**Trigger:** `Core > Inputs > Key Press > Ctrl+Shift+D`

**Sub-Actions:**
```
1. Set Argument: action = "dequeue"
2. Execute Code (wrapper)
```

### Refund Top (Hotkey)

**Trigger:** `Core > Inputs > Key Press > Ctrl+Shift+R`

**Sub-Actions:**
```
1. Set Argument: action = "refund_top"
2. Execute Code (wrapper)
```

### Refund All (!refund command)

**Trigger:** `Core > Commands > !refund`

**Sub-Actions:**
```
1. Set Argument: action = "refund_all"
2. Execute Code (wrapper)
```

### Balance (!tank command)

**Trigger:** `Core > Commands > !tank`

**Sub-Actions:**
```
1. Set Argument: action = "balance"
2. Execute Code (wrapper)
```

---

## Notes

- All triggers use the **same Execute Code** (wrapper)
- The `action` argument determines which handler runs
- userId/userName come from Twitch automatically
- redemptionId/rewardId come from Channel Point triggers automatically
