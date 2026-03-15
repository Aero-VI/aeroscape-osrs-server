# Security Review — AeroScape Login Protocol Handler

**Reviewer:** Cipher (Chief Security Officer, Aeroverra)  
**Date:** 2026-03-15  
**Scope:** `AeroScape.LoginServer` project — `LoginHandler.cs`, `RsaKeys.cs`, `IsaacCipher.cs`, `Xtea.cs`, `Program.cs`  
**Phase:** AeroScape Phase 1 (Proof of Concept)

---

## Executive Summary

Kai's implementation correctly follows the OSRS login protocol specification authored by Avery. The code is readable, well-structured, and demonstrates a solid understanding of the handshake flow. **No critical vulnerabilities that would compromise the Phase 1 PoC** have been identified. However, several medium and low severity issues exist that **must be addressed before any production or wider testing deployment**. Two findings are flagged as blocking for Phase 2 work.

---

## Findings

### 🔴 BLOCKING — Must Fix Before Phase 2

---

#### [SEC-01] Negative `xteatLen` — Potential Integer Underflow / Crash

**File:** `LoginHandler.cs` — Phase 5 XTEA block  
**Severity:** High (Denial of Service / unexpected behavior)

**Description:**

```csharp
int headerConsumed = 4 + 4 + 1 + 2 + rsaBlockSize;
int xteatLen = payloadSize - headerConsumed;

if (xteatLen > 0)
{
    byte[] xteaBytes = await ReadBytesAsync(stream, xteatLen);
```

`payloadSize` is a client-supplied `u16`. If a malicious or malformed client sends a `payloadSize` smaller than `headerConsumed`, `xteatLen` will be negative. The `> 0` guard prevents `ReadBytesAsync` from being called, so there is no crash here — but the resulting state is silently inconsistent (partially consumed stream). Additionally, `ReadBytesAsync` with a negative `count` would pass through `ReadExactAsync` and allocate a `new byte[count]` — which in .NET with a negative integer throws `OverflowException` at the array allocation, not a controlled rejection.

**Fix:**

```csharp
if (xteatLen < 0)
{
    Console.WriteLine($"[{_remoteEndpoint}] Payload size underflow (payloadSize={payloadSize}, consumed={headerConsumed}).");
    await SendByteAsync(stream, ResponseMalformed);
    return;
}
```

---

#### [SEC-02] Private Key Material Exposed as Public Static Property

**File:** `RsaKeys.cs`  
**Severity:** High (Key Compromise)

**Description:**

```csharp
public static BigInteger PrivateExp   { get; private set; }
```

The RSA private exponent is exposed as a public static property with no access restriction beyond the setter. Any code within the process — including future plugins, extensions, or a compromised dependency — can trivially read the private key. Additionally, `SaveKey()` serializes the private key using `_rsa!.ToXmlString(includePrivateParameters: true)`, writing `<D>`, `<P>`, `<Q>`, `<DP>`, `<DQ>`, and `<InverseQ>` to a plaintext XML file on disk with no file permission controls.

**Fixes:**

1. Remove the `PrivateExp` public property. Keep `_rsa` private and expose only a `Decrypt(byte[])` method (already exists — good). Remove the `BigInteger`-based `PrivateExp` field entirely.
2. Set restrictive file permissions on `server_rsa.xml` at creation time (mode 0600 on Linux).
3. Consider storing the key in the OS secret store (`DPAPI` on Windows, `libsecret`/keychain on Linux/macOS) rather than a plaintext XML file.

---

### 🟡 MEDIUM — Address Before External Testing

---

#### [SEC-03] No Rate Limiting or Connection Throttling

**File:** `Program.cs`  
**Severity:** Medium (Denial of Service)

**Description:** The server accepts unlimited concurrent TCP connections from any IP. Each connection spawns an unbounded `Task.Run`. A single attacker can exhaust server memory and thread pool resources by opening thousands of connections.

**Fix:** Implement per-IP connection rate limiting and a global concurrent connection cap (e.g., 2047 — the max player slot count). Consider using a `SemaphoreSlim` to cap in-flight login handlers.

---

#### [SEC-04] No Read Timeout — Slowloris-Style Attack

**File:** `LoginHandler.cs`  
**Severity:** Medium (Denial of Service)

**Description:** `ReadExactAsync` and `ReadByteAsync` will block indefinitely if the client connects and sends data one byte at a time or stalls mid-handshake. A client that never completes the handshake holds a live task and network stream open forever.

**Fix:** Set `_client.ReceiveTimeout` on the `TcpClient` immediately after accept, or wrap reads in a `CancellationTokenSource` with a timeout (e.g., 10 seconds for the full handshake).

---

#### [SEC-05] Unbounded `ReadNullTerminatedString` — Controlled Memory Pressure

**File:** `LoginHandler.cs`  
**Severity:** Medium (DoS / Logic Error)

**Description:**

```csharp
private static string ReadNullTerminatedString(byte[] buf, ref int pos)
{
    int start = pos;
    while (pos < buf.Length && buf[pos] != 0x00 && buf[pos] != 0x0A)
        pos++;
    string s = Encoding.ASCII.GetString(buf, start, pos - start);
```

If the RSA plaintext contains no null terminator for username or password, the parser will consume the entire remaining buffer as a single string. RSA block size is capped at 512 bytes (good), so this is not a classic overflow — but it means a crafted block with no terminators will produce usernames/passwords up to 512 bytes long, potentially causing issues in downstream DB lookups or logging. Per protocol spec, maximum username length is 12 characters and password is 20.

**Fix:** Add length guards:

```csharp
// In ReadNullTerminatedString, after the while loop:
if (pos - start > 20) // max of username (12) or password (20)
    throw new InvalidDataException("String field exceeds maximum length.");
```

---

#### [SEC-06] Server Seed Not Incorporated Into ISAAC Seeds

**File:** `LoginHandler.cs` — Phase 7  
**Severity:** Medium (Replay Attack Surface)

**Description:** The ISAAC ciphers are seeded purely from the four client-supplied integers extracted from the RSA block:

```csharp
var inCipher  = new IsaacCipher(isaacSeeds);
var outCipher = new IsaacCipher(new int[]
{
    isaacSeeds[0] + 50, isaacSeeds[1] + 50,
    isaacSeeds[2] + 50, isaacSeeds[3] + 50
});
```

The server seed (generated in Phase 2) is sent to the client but never incorporated server-side into the ISAAC state. In classic RS2 (rev 317), the server seed was XORed into the client seeds before ISAAC initialization. If the client follows the classic protocol and incorporates the server seed into the RSA block content, the seeds will match. If not, the ISAAC streams will diverge silently. More importantly, without binding the ISAAC seeds to the server-generated nonce, a passive attacker who has previously recorded a valid RSA-encrypted login block could replay it to establish a predictable cipher stream (the server seed changes each connection, but the ISAAC output would be identical if seeds are not bound to it).

**Fix:** Per Avery's spec (§9 and Appendix B §3), confirm whether the client incorporates the server seed into the RSA block seeds or whether the server must XOR them in at initialization. Align server-side ISAAC seeding accordingly and document the decision.

---

#### [SEC-07] `_nextPlayerIndex` Slot Allocation — Race Condition

**File:** `LoginHandler.cs`  
**Severity:** Medium (Logic Error under concurrent load)

**Description:**

```csharp
int playerIndex = System.Threading.Interlocked.Increment(ref _nextPlayerIndex) % 2047;
if (playerIndex == 0) playerIndex = 1; // index 0 is reserved
```

`Interlocked.Increment` is atomic, but the `% 2047` and the subsequent guard are not. Two threads could simultaneously receive `playerIndex = 0` from the modulo (when the counter hits a multiple of 2047) and both reassign to `1`, producing two sessions with slot `1`. Additionally, there is no actual slot occupancy check — the server accepts logins into "slots" without verifying whether that slot is already occupied.

**Fix:** For Phase 1 this is tolerable as there is no game world state. For Phase 2, replace with a proper slot pool (e.g., `ConcurrentQueue<int>` pre-populated with indices 1–2047).

---

### 🟢 LOW — Informational / Best Practice

---

#### [SEC-08] 1024-bit RSA Key Size

**File:** `RsaKeys.cs`  
**Severity:** Low (Acceptable for PoC; insufficient for production)

**Description:** 1024-bit RSA is considered below the current NIST recommended minimum of 2048 bits. It matches the OSRS reference implementation and is adequate for a controlled development environment, but should be upgraded to 2048 bits before any internet-facing deployment.

**Recommendation:** Change `RSA.Create(1024)` to `RSA.Create(2048)` and re-patch the client modulus before external testing.

---

#### [SEC-09] No Padding — Textbook RSA

**File:** `RsaKeys.cs` — `Decrypt()`  
**Severity:** Low (Protocol Design — not a code defect)

**Description:** RSA decryption uses raw `BigInteger.ModPow` with no PKCS#1 or OAEP padding, mirroring the OSRS protocol design. Textbook RSA is malleable and vulnerable to chosen-ciphertext attacks. Since the RSA block content is validated (magic byte check), the attack surface is limited in practice. This is an inherited protocol constraint.

**Recommendation:** Document this as a known protocol limitation. Do not attempt to add padding — the client will not produce padded ciphertext and decryption will fail.

---

#### [SEC-10] Credentials Visible in Log / Console Output

**File:** `LoginHandler.cs`  
**Severity:** Low (Operational Security)

**Description:**

```csharp
Console.WriteLine($"[{_remoteEndpoint}] Login attempt — user: '{username}' uid: {uid}");
```

Username is logged in plaintext. Passwords are not logged (correct). Logging usernames in plaintext is acceptable for development but should be reviewed before any shared or persistent logging infrastructure is introduced.

**Recommendation:** For Phase 2, consider hashing or truncating usernames in logs, and ensure log files are not world-readable.

---

#### [SEC-11] Game Traffic Loop — Single-Buffer Opcode Unmasking

**File:** `LoginHandler.cs` — `GameTrafficLoopAsync`  
**Severity:** Low (Phase 1 placeholder — does not affect current functionality)

**Description:**

```csharp
byte[] buf = new byte[4096];
// ...
int maskedOpcode = buf[0] & 0xFF;
int rawOpcode    = (maskedOpcode - inCipher.NextInt()) & 0xFF;
```

`stream.ReadAsync` may return multiple coalesced packets in a single call. The loop calls `inCipher.NextInt()` exactly once per `ReadAsync` call, consuming one cipher value regardless of how many packets arrived. When real game packet processing is implemented, each packet's opcode must be unmasked with its own `NextInt()` call, sequentially. The current placeholder will misalign the cipher stream as soon as more than one packet arrives in a single read.

**Recommendation:** This is expected Phase 1 placeholder behavior. Flag for replacement in Phase 2 game loop implementation.

---

#### [SEC-12] XTEA Decryption Failure Is Non-Fatal

**File:** `LoginHandler.cs`  
**Severity:** Low (Correctness)

**Description:**

```csharp
catch (Exception ex)
{
    Console.WriteLine($"[{_remoteEndpoint}] XTEA decryption warning: {ex.Message}");
    // Non-fatal — continue with login
}
```

XTEA decryption failure is swallowed and login proceeds. In Phase 1 this is intentional (client metadata is not required). In Phase 2, if cache checksums or anti-cheat data from the XTEA block become required, a silent failure here would allow clients with corrupted/tampered XTEA blocks to proceed.

**Recommendation:** Document this as a known Phase 1 relaxation. Revisit in Phase 2 if XTEA block fields become mandatory.

---

## Summary Table

| ID | Severity | Title | Blocking? |
|----|----------|-------|-----------|
| SEC-01 | 🔴 High | Negative `xteatLen` integer underflow | **Yes** |
| SEC-02 | 🔴 High | Private key exposed as public property | **Yes** |
| SEC-03 | 🟡 Medium | No rate limiting / connection cap | No |
| SEC-04 | 🟡 Medium | No read timeout (slowloris) | No |
| SEC-05 | 🟡 Medium | Unbounded `ReadNullTerminatedString` | No |
| SEC-06 | 🟡 Medium | Server seed not bound to ISAAC seeds | No |
| SEC-07 | 🟡 Medium | Player slot allocation race condition | No |
| SEC-08 | 🟢 Low | 1024-bit RSA key | No |
| SEC-09 | 🟢 Low | Textbook RSA (no padding) | No |
| SEC-10 | 🟢 Low | Usernames in plaintext logs | No |
| SEC-11 | 🟢 Low | Single-buffer opcode unmasking in game loop | No |
| SEC-12 | 🟢 Low | XTEA failure non-fatal | No |

---

## Sign-Off

**Phase 1 PoC: CONDITIONAL SIGN-OFF ✅**

The implementation is acceptable for internal Phase 1 proof-of-concept testing with the following conditions:

1. **SEC-01 must be patched** before the server is exposed to any non-trusted client (trivially exploitable for DoS).
2. **SEC-02 must be addressed** before the RSA private key is considered production material — the current key file and public property exposure are unacceptable for any key that signs real client sessions.

All other findings are informational for this phase and tracked for Phase 2 remediation.

Good execution on the protocol fundamentals, Kai. The RSA/ISAAC/XTEA pipeline is structurally correct and the defensive input validation on the RSA block size and magic byte check are exactly right.

— **Cipher**  
*Chief Security Officer, Aeroverra*
