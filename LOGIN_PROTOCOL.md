# OSRS Login Handshake Protocol — AeroScape Reference

**Author:** Avery (RuneLite Engineer, Aeroverra)  
**Date:** 2026-03-15  
**Source:** Reverse-engineered from `Aeroverra/runelite` deob fork + community RSPS research  
**Audience:** Backend developer (Kai) implementing a compatible login server handler

---

## Table of Contents

1. [Overview](#1-overview)
2. [Transport Layer](#2-transport-layer)
3. [Phase 1 — Initial Handshake (JS5 / Login Selector)](#3-phase-1--initial-handshake)
4. [Phase 2 — Server Hello](#4-phase-2--server-hello)
5. [Phase 3 — Login Request Packet](#5-phase-3--login-request-packet)
6. [Phase 4 — RSA-Encrypted Login Block](#6-phase-4--rsa-encrypted-login-block)
7. [Phase 5 — XTEA-Encrypted Remainder Block](#7-phase-5--xtea-encrypted-remainder-block)
8. [Phase 6 — Server Login Response](#8-phase-6--server-login-response)
9. [Phase 7 — ISAAC Cipher Initialization](#9-phase-7--isaac-cipher-initialization)
10. [Phase 8 — Post-Login Game Traffic](#10-phase-8--post-login-game-traffic)
11. [Cryptographic Details](#11-cryptographic-details)
12. [Response Code Reference](#12-response-code-reference)
13. [Reconnect Flow](#13-reconnect-flow)
14. [Implementation Checklist for Kai](#14-implementation-checklist-for-kai)

---

## 1. Overview

The OSRS login protocol is a multi-phase TCP handshake that:

- Establishes which service the client wants (login vs. cache update)
- Exchanges a server-generated seed for session uniqueness
- Encrypts sensitive credentials (password, session keys) inside an **RSA block**
- Encrypts the remaining client metadata with **XTEA** keyed from the session keys
- Initialises two **ISAAC** stream ciphers for ongoing game packet opcode masking

### High-Level Sequence

```
Client                                  Server
  │                                        │
  │──── [1 byte] Handshake Opcode ────────►│  (14 = login service)
  │                                        │
  │◄─── [8 bytes] Server Seed ─────────────│
  │◄─── [1 byte]  Padding (0x00) ──────────│  (some revisions)
  │                                        │
  │──── Login Request Packet ─────────────►│
  │     • Login type byte (16 or 18)       │
  │     • Packet size (u16)                │
  │     • Client revision (i32)            │
  │     • RSA block (size-prefixed)        │
  │     • XTEA-encrypted remainder         │
  │                                        │
  │◄─── [1 byte] Response Code ────────────│  (2 = OK)
  │◄─── [3 bytes] Player metadata ─────────│  (rights + player index, on OK)
  │                                        │
  │  [Both sides init ISAAC ciphers]       │
  │                                        │
  │════════ Encrypted Game Traffic ════════│
```

---

## 2. Transport Layer

- **Protocol:** TCP (raw sockets, not HTTP)
- **Default Port:** 43594 (world-dependent, worlds 300–500 use 43594 + world offset in some configs)
- **Byte Order:** Big-endian (network byte order) throughout
- **Framing:** After login, game packets use a variable-length framing described in §10

---

## 3. Phase 1 — Initial Handshake

### Client → Server: Service Selector (1 byte)

The very first byte the client sends selects the service:

| Value | Meaning |
|-------|---------|
| `14`  | **Login service** (normal game login) |
| `15`  | **Update/JS5 service** (cache update) |

From `HandshakeType.java` in the RuneLite fork:

```java
public enum HandshakeType {
    LOGIN(14),
    UPDATE(15);
}
```

For the login flow, the client sends `0x0E` (decimal 14).

**The server must read exactly 1 byte and branch on its value.** If it is not 14 or 15, close the connection.

---

## 4. Phase 2 — Server Hello

After receiving `0x0E`, the server sends its **Server Hello** response.

### Server → Client: Server Hello

| Offset | Size | Type | Description |
|--------|------|------|-------------|
| 0      | 1    | `u8` | Status byte — always `0x00` (OK, proceed) |
| 1      | 8    | `i64`| **Server seed** — a random 64-bit integer |

Total: **9 bytes**

```
[0x00][seed_hi: i32][seed_lo: i32]
```

**Implementation note:** Generate the server seed with a CSPRNG (e.g., `SecureRandom`). This seed is combined with the client-generated seed inside the login block to form the ISAAC seed array (see §11).

**The server must store the server seed in the session state** — it is needed later for ISAAC initialisation.

---

## 5. Phase 3 — Login Request Packet

After receiving the server hello, the client builds and sends the login request packet.

### Client → Server: Login Request

| Offset | Size   | Type  | Description |
|--------|--------|-------|-------------|
| 0      | 1      | `u8`  | **Login type** — `16` = new login, `18` = reconnect |
| 1      | 2      | `u16` | **Payload size** — total byte count of everything that follows |
| 3      | 4      | `i32` | **Client revision** (e.g., 225, 226 — the game build number) |
| 7      | 4      | `i32` | **Sub-version** (cache revision / sub-build; may be 1 or 0) |
| 11     | 1      | `u8`  | **Client type** — `1` = Java, `2` = C++; use `1` for standard |
| 12     | 2      | `u16` | **RSA block size** — byte count of the RSA-encrypted block that follows |
| 14     | varies | bytes | **RSA block** — see §6 |
| 14+N   | varies | bytes | **XTEA-encrypted remainder** — see §7 |

> **Revision note:** The exact field layout can vary slightly by revision. The above matches the layout from roughly revision 180–230+. Always validate that `client_revision` matches your server's expected value and reject mismatches with response code `6` (OUTDATED).

**Login types:**
- `16` — Standard new-session login
- `18` — Reconnect (player dropped; re-establishing existing session)

---

## 6. Phase 4 — RSA-Encrypted Login Block

This is the security-critical section. The client RSA-encrypts a block containing the session keys and password before sending it.

### Pre-Encryption Plaintext Block Structure

| Offset | Size  | Type     | Description |
|--------|-------|----------|-------------|
| 0      | 1     | `u8`     | **Magic byte** — always `0x0A` (decimal 10) |
| 1      | 4     | `i32`    | **ISAAC seed[0]** — client random |
| 5      | 4     | `i32`    | **ISAAC seed[1]** — client random |
| 9      | 4     | `i32`    | **ISAAC seed[2]** — client random |
| 13     | 4     | `i32`    | **ISAAC seed[3]** — client random |
| 17     | 8     | `i64`    | **UID / device fingerprint** — 0 in early revisions |
| 25     | ~N    | `string` | **Username** — null-terminated or `\n`-terminated (see note) |
| 25+N   | ~M    | `string` | **Password** — null-terminated or `\n`-terminated |

> **String encoding note:** In classic RS2/OSRS clients, strings in the login block are written as null-terminated ASCII. Some revisions use a custom terminator (0x0A). Username max length is 12 characters; password max is 20.

The 4 × `i32` values form the **client session key** — 128 bits total of cryptographic randomness the client generates fresh for every login attempt.

### RSA Encryption

1. Assemble the plaintext block above into a byte array.
2. Treat the byte array as a **big-endian unsigned integer** (i.e., `new BigInteger(1, bytes)`).
3. Encrypt: `ciphertext = plaintext.modPow(publicExponent, modulus)`.
4. Convert the resulting BigInteger back to a byte array (big-endian).
5. Prepend a `u16` length prefix (the `RSA block size` field in §5).

**The client uses Jagex's RSA public key** — for AeroScape you must patch the client to use your own RSA key pair (see §11).

### Server-Side RSA Decryption

```
BigInteger encrypted = new BigInteger(rsaBytes);
BigInteger decrypted = encrypted.modPow(privateExponent, modulus);
byte[] plaintext = decrypted.toByteArray();
```

After decryption, read the plaintext sequentially per the table above. **Validate the magic byte is `0x0A`** — if it is not, the RSA decryption failed (wrong key, corrupt packet) — reject with `MALFORMED_PACKET (22)`.

---

## 7. Phase 5 — XTEA-Encrypted Remainder Block

Immediately after the RSA block comes a **XTEA-encrypted** section containing the client's metadata (machine info, display settings, cache checksums, etc.).

### XTEA Decryption Key

The XTEA key is the **four ISAAC seed integers** extracted from the RSA block:

```
int[4] xteaKey = { isaacSeed[0], isaacSeed[1], isaacSeed[2], isaacSeed[3] }
```

### XTEA-Encrypted Block Contents (typical, revision-dependent)

| Field | Type | Description |
|-------|------|-------------|
| Display mode | `u8` | `0` = fixed, `1` = resizable, `2` = fullscreen |
| Canvas width | `u16` | Client canvas width in pixels |
| Canvas height | `u16` | Client canvas height in pixels |
| Anti-aliasing mode | `u8` | |
| UID | `i64` | Machine/device unique identifier |
| Token string | `string` | Session token (null-terminated) |
| Affiliate ID | `i32` | Affiliate / referral ID |
| Settings | `i32` | Bitmask of client settings |
| Client OS | `u8` | `1` = Windows, `2` = macOS, `3` = Linux |
| Client is 64-bit | `u8` | Boolean |
| Client version | `u8` | Internal client version info |
| Cache checksums | `i32[17]` | CRC32 of each cache index file (indices 0–16) |

> **Note:** The exact XTEA block layout varies by revision and Jagex updates it frequently. For AeroScape, you only need to parse fields your server cares about (display mode, UID for anti-botting). The critical security-sensitive fields (credentials + session keys) are in the RSA block.

### XTEA Algorithm Reference

XTEA is a 64-bit block cipher with a 128-bit key, 64 rounds (32 full rounds). Standard implementation:

```java
public static void decrypt(int[] block, int[] key) {
    int v0 = block[0], v1 = block[1];
    int sum = 0xC6EF3720; // delta * 32
    int delta = 0x9E3779B9;
    for (int i = 0; i < 32; i++) {
        v1 -= ((v0 << 4 ^ v0 >>> 5) + v0) ^ (sum + key[sum >>> 11 & 3]);
        sum -= delta;
        v0 -= ((v1 << 4 ^ v1 >>> 5) + v1) ^ (sum + key[sum & 3]);
    }
    block[0] = v0;
    block[1] = v1;
}
```

Decrypt the raw bytes in 8-byte blocks using the XTEA key derived from ISAAC seed.

---

## 8. Phase 6 — Server Login Response

After parsing and validating the login packet, the server sends a response.

### Server → Client: Response (Success)

| Offset | Size | Type | Description |
|--------|------|------|-------------|
| 0      | 1    | `u8` | **Response code** — `2` = success (logged in) |
| 1      | 1    | `u8` | **Rights** — `0` = player, `1` = mod, `2` = admin |
| 2      | 1    | `u8` | **Unknown / padding** — typically `0` |
| 3      | 2    | `u16`| **Player index** — the player's slot in the world (1–2047) |

> **Older revisions** use a 2-byte response on success (just the response code + rights). Modern OSRS uses the 5-byte format above.

### Server → Client: Response (Failure)

| Offset | Size | Type | Description |
|--------|------|------|-------------|
| 0      | 1    | `u8` | **Response code** — see §12 for full list |

On failure, the server sends only 1 byte. The connection may be closed immediately after.

---

## 9. Phase 7 — ISAAC Cipher Initialization

After a successful login response, **both the client and server initialize ISAAC cipher pairs** for ongoing game packet encryption.

### ISAAC Seed Construction

The ISAAC seed is a 4-element `int[]` derived from both the client session keys and the server seed:

```java
int[] isaacSeed = new int[4];
isaacSeed[0] = clientSeed[0];   // from RSA block
isaacSeed[1] = clientSeed[1];   // from RSA block
isaacSeed[2] = clientSeed[2];   // from RSA block
isaacSeed[3] = clientSeed[3];   // from RSA block
```

Where `clientSeed[i]` are the four `i32` values extracted from the RSA block.

> **Note on the server seed:** In classic RS2 (revision 317), the server seed (received in the Server Hello) was **also** incorporated into the ISAAC seed. In OSRS (post-2013), the server seed is transmitted to allow the client to use it in combination with client-generated randoms. The precise combination is: the client uses both the 8-byte server seed and its own 4 random ints — the isaac seed array in the RSA block already incorporates both in modern clients. What the server must do is use **the same four ints it extracted from the RSA block** as the seed.

### Two ISAAC Instances

Two separate ISAAC instances are created:

| Instance | Seed Derivation | Purpose |
|----------|-----------------|---------|
| **Incoming cipher** (server-side decoder) | `seed[i]` as-is | Server uses to decode client packet opcodes |
| **Outgoing cipher** (server-side encoder) | `seed[i] + 50` | Server uses to encode server→client packet opcodes |

```java
// Server side
int[] inSeed  = { seed[0],      seed[1],      seed[2],      seed[3]      };
int[] outSeed = { seed[0] + 50, seed[1] + 50, seed[2] + 50, seed[3] + 50 };

ISAACCipher decodingCipher = new ISAACCipher(inSeed);   // decode client packets
ISAACCipher encodingCipher = new ISAACCipher(outSeed);  // encode server packets
```

The client mirrors this — it creates an **encoding cipher** seeded with the same seeds (to encode its outgoing opcodes) and a **decoding cipher** seeded with `+50` (to decode server opcodes). The `+50` offset creates two complementary but distinct cipher streams.

### ISAAC Algorithm Reference

ISAAC (Indirection, Shift, Accumulate, Add, and Count) is a CSPRNG. The implementation used in OSRS:

```java
public class ISAACCipher {
    private static final int GOLDEN_RATIO = 0x9E3779B9;
    private int[] results = new int[256];
    private int[] mem    = new int[256];
    private int count, a, b, c;

    public ISAACCipher(int[] seed) {
        // initialization and seeding
        a = b = c = 0;
        int[] initArr = new int[8];
        Arrays.fill(initArr, GOLDEN_RATIO);
        for (int i = 0; i < 4; i++) mix(initArr);
        for (int i = 0; i < 256; i += 8) {
            if (i < seed.length) {
                for (int j = 0; j < 8; j++)
                    initArr[j] += (j + i < seed.length) ? seed[j + i] : 0;
            }
            mix(initArr);
            System.arraycopy(initArr, 0, mem, i, 8);
        }
        generate();
        count = 256;
    }

    public int nextInt() {
        if (count-- == 0) { generate(); count = 255; }
        return results[count];
    }

    // internal generate() and mix() per standard ISAAC spec
}
```

A complete, tested Java implementation is widely available; do not implement ISAAC from scratch without thorough testing.

### Opcode Masking

For each game packet sent **from the client**, the client XORs the raw opcode with the next value from its encoding ISAAC cipher:

```
maskedOpcode = (rawOpcode + isaacNext()) & 0xFF
```

The server reverses this:

```
rawOpcode = (maskedOpcode - isaacNext()) & 0xFF
```

For each game packet sent **from the server**, the same masking applies using the server's encoding cipher, and the client decodes with its decoding cipher.

---

## 10. Phase 8 — Post-Login Game Traffic

After ISAAC ciphers are established, game packets flow in both directions. Each packet has the form:

### Fixed-Size Packet

```
[opcode: u8 (masked)] [payload: N bytes]
```

### Variable-Size Packet (byte-length prefix)

```
[opcode: u8 (masked)] [size: u8] [payload: size bytes]
```

### Variable-Size Packet (short-length prefix)

```
[opcode: u8 (masked)] [size: u16] [payload: size bytes]
```

The server must know the expected size of each opcode from a packet size table (derived from client deobfuscation). The opcode must be unmasked using ISAAC before looking up the size.

---

## 11. Cryptographic Details

### RSA Key Pair for AeroScape

**You must generate your own RSA key pair and patch the client to use it.**

Jagex uses a 512-bit RSA key (older clients) or 1024-bit (newer OSRS clients). For AeroScape:

1. Generate a 1024-bit RSA key pair.
2. Extract the **modulus** and **public exponent** (usually `65537`).
3. Patch the deobfuscated client: replace Jagex's hardcoded modulus with yours.
4. The server holds the **private key** for decryption.

**Key generation (Java):**

```java
KeyPairGenerator gen = KeyPairGenerator.getInstance("RSA");
gen.initialize(1024);
KeyPair pair = gen.generateKeyPair();
RSAPublicKey  pub  = (RSAPublicKey)  pair.getPublic();
RSAPrivateKey priv = (RSAPrivateKey) pair.getPrivate();

BigInteger modulus  = pub.getModulus();          // patch into client
BigInteger pubExp   = pub.getPublicExponent();   // patch into client (65537)
BigInteger privExp  = priv.getPrivateExponent(); // keep secret on server
```

**Server decryption:**

```java
BigInteger encryptedBlock = new BigInteger(1, rsaBytes);
BigInteger decryptedBlock = encryptedBlock.modPow(privExp, modulus);
byte[] plaintext = toByteArray(decryptedBlock); // strip leading zero if present
```

### RSA Modulus Location in Client

Using the RuneLite deobfuscation tools, search the decompiled client bytecode for:
- A `BigInteger` constructed from a large hex string literal
- Fields named near login/handshake code
- The `modPow` call site in the login method

Patch by replacing the byte-array literal or hex string with your own modulus bytes.

### XTEA Algorithm

Standard 64-round XTEA. See §7 for the reference implementation. The key is the 4-int ISAAC seed from the RSA block.

### ISAAC Cipher

Standard ISAAC algorithm (Bob Jenkins, 1996). The Java reference implementation in the OSRS RSPS community is well-established — use a vetted version. Key properties:
- 256-element internal state array
- Seeded with a `int[]` array
- `nextInt()` produces the next pseudo-random value
- Not cryptographically strong by modern standards, but sufficient for opcode masking

---

## 12. Response Code Reference

From `HandshakeResponseType.java` in the RuneLite fork:

| Code | Constant | Meaning |
|------|----------|---------|
| 0    | `RESPONSE_OK` | Generic OK (pre-login) |
| 2    | `LOGGED_IN` | ✅ Login successful |
| 3    | `INVALID_USERNAME_OR_PASSWORD` | Wrong credentials |
| 4    | `ACCOUNT_DISABLED` | Account banned |
| 5    | `ACCOUNT_ONLINE` | Already logged in |
| 6    | `RESPONSE_OUTDATED` | Client revision mismatch |
| 7    | `WORLD_FULL` | Server at capacity |
| 8    | `SERVER_OFFLINE` | Login server unavailable |
| 9    | `LIMITED_EXCEEDED` | Rate limit / too many from this IP |
| 10   | `BAD_SESSION_ID` | Session ID invalid |
| 11   | `ACCOUNT_HIJACK` | Account flagged for suspicious activity |
| 12   | `MEMBERS_WORLD` | Members-only world |
| 13   | `COULD_NOT_COMPLETE_LOGIN` | Generic login failure |
| 14   | `SERVER_BEING_UPDATED` | Server restarting |
| 16   | `TOO_MANY_ATTEMPTS` | Too many failed login attempts |
| 17   | `MEMBERS_ONLY_AREA` | Members area access denied |
| 18   | `ACCOUNT_LOCKED` | Account locked |
| 19   | `CLOSED_BETA` | Closed beta restriction |
| 20   | `INVALID_LOGINSERVER` | Invalid login server |
| 21   | `PROFILE_TRANSFER` | Profile transfer in progress |
| 22   | `MALFORMED_PACKET` | ❌ Packet parse failure (bad RSA decrypt, bad magic) |
| 23   | `NO_REPLY_FROM_LOGINSERVER` | Auth server timeout |
| 24   | `ERR_LOADING_PROFILE` | Profile load failure |
| 25   | `UNEXPECTED_LOGINSERVER_RESPONSE` | Unexpected auth server response |
| 26   | `IP_BANNED` | IP address banned |
| 27   | `SERVICE_UNAVAILABLE` | Service unavailable |
| 31   | `NO_DISPLAY_NAME` | No display name set |
| 32   | `BILLING_ERROR` | Billing/subscription issue |
| 37   | `ACCOUNT_INACCESSABLE` | Account inaccessible |
| 38   | `VOTE_TO_PLAY` | Vote required |
| 55   | `NOT_ELIGIBLE` | Not eligible for this content |
| 56   | `NEED_AUTHENTICATOR` | 2FA required |
| 57   | `AUTHENTICATOR_CODE_WRONG` | Wrong 2FA code |

---

## 13. Reconnect Flow

When `login type` is `18` (reconnect), the protocol is nearly identical with these differences:

1. The client sends login type `18` instead of `16`.
2. The client includes its **previous session's ISAAC seed** in the RSA block (so the server can re-establish the cipher pair without a full re-auth).
3. The server should validate the reconnect seed against the stored session state for that player.
4. On success, the server sends response code `2` and the session ciphers are re-initialized.
5. The player's in-game state is preserved (they never fully disconnected from the game world perspective).

---

## 14. Implementation Checklist for Kai

Use this checklist when implementing the login handler:

### Network Layer
- [ ] Accept raw TCP connections on port `43594` (configurable)
- [ ] Use Netty or similar for async I/O
- [ ] Set `TCP_NODELAY` to avoid Nagle algorithm delays on login packets

### Phase 1 — Handshake
- [ ] Read 1 byte; validate it is `14` (login) or `15` (update service)
- [ ] Branch to appropriate handler; close connection on unknown opcode

### Phase 2 — Server Hello
- [ ] Generate 8-byte random server seed with `SecureRandom`
- [ ] Send `[0x00][seed: 8 bytes]` (9 bytes total)
- [ ] Store server seed in session state

### Phase 3 — Login Request Header
- [ ] Read login type byte (`16` or `18`)
- [ ] Read `u16` payload size
- [ ] Read `i32` client revision → reject with code `6` if mismatch
- [ ] Read `i32` sub-version
- [ ] Read `u8` client type
- [ ] Read `u16` RSA block size

### Phase 4 — RSA Block
- [ ] Read exactly `rsaBlockSize` bytes
- [ ] Decrypt with your RSA private key via `BigInteger.modPow`
- [ ] Validate magic byte is `0x0A` → reject with code `22` on failure
- [ ] Extract 4 × `i32` ISAAC seeds → store in session state
- [ ] Extract `i64` UID → store for logging/anti-cheat
- [ ] Extract username (null-terminated string) → normalize to lowercase
- [ ] Extract password (null-terminated string) → hash/validate against DB

### Phase 5 — XTEA Remainder
- [ ] Decrypt remaining bytes with XTEA using the 4-int ISAAC seed as key
- [ ] Parse display mode, canvas size, cache CRCs as needed

### Phase 6 — Authentication
- [ ] Look up username in player database
- [ ] Validate password hash (bcrypt or similar recommended)
- [ ] Check account status (banned, locked, online)
- [ ] Allocate a player index slot (1–2047)
- [ ] Send response: `[2][rights][0x00][playerIndex: u16]` on success
- [ ] Send single-byte error code on failure

### Phase 7 — ISAAC Init
- [ ] Init incoming cipher with seeds `[s0, s1, s2, s3]`
- [ ] Init outgoing cipher with seeds `[s0+50, s1+50, s2+50, s3+50]`
- [ ] Attach ciphers to the player's network session

### Phase 8 — Game Traffic
- [ ] For every incoming packet: unmask opcode with `incoming.nextInt()`
- [ ] For every outgoing packet: mask opcode with `outgoing.nextInt()`
- [ ] Use opcode-to-size table to frame incoming packets correctly

---

## Appendix A: Example Login Handler Pseudocode (Kotlin/Java-style)

```kotlin
fun handleLogin(session: Session) {
    // Phase 1
    val serviceType = session.readByte()
    require(serviceType == 14) { "Not a login service request" }

    // Phase 2
    val serverSeed = SecureRandom.getInstanceStrong().nextLong()
    session.writeByte(0x00)
    session.writeLong(serverSeed)
    session.flush()

    // Phase 3
    val loginType = session.readByte()        // 16 or 18
    val payloadSize = session.readShort()
    val clientRevision = session.readInt()
    if (clientRevision != EXPECTED_REVISION) {
        session.writeByte(6); session.close(); return
    }
    val subVersion = session.readInt()
    val clientType  = session.readByte()
    val rsaBlockSize = session.readShort()

    // Phase 4 — RSA
    val rsaBytes = session.readBytes(rsaBlockSize)
    val encrypted = BigInteger(1, rsaBytes)
    val decrypted = encrypted.modPow(RSA_PRIVATE_EXP, RSA_MODULUS)
    val plaintext = decrypted.toByteArray().trimLeadingZero()

    val buf = ByteBuffer.wrap(plaintext)
    val magic = buf.get()
    if (magic != 0x0A.toByte()) {
        session.writeByte(22); session.close(); return
    }
    val isaacSeeds = IntArray(4) { buf.int }
    val uid = buf.long
    val username = buf.readNullString()
    val password = buf.readNullString()

    // Phase 5 — XTEA (parse remainder, omitted for brevity)

    // Phase 6 — Auth
    val player = db.lookupPlayer(username) ?: run {
        session.writeByte(3); session.close(); return
    }
    if (!verifyPassword(password, player.passwordHash)) {
        session.writeByte(3); session.close(); return
    }
    val playerIndex = world.allocateSlot() ?: run {
        session.writeByte(7); session.close(); return
    }
    session.writeByte(2)           // success
    session.writeByte(player.rights)
    session.writeByte(0)
    session.writeShort(playerIndex)
    session.flush()

    // Phase 7 — ISAAC
    val inCipher  = ISAACCipher(isaacSeeds)
    val outCipher = ISAACCipher(IntArray(4) { isaacSeeds[it] + 50 })
    session.attachCiphers(inCipher, outCipher)

    // Phase 8 — Hand off to game loop
    world.onPlayerLogin(player, playerIndex, session)
}
```

---

## Appendix B: Common Pitfalls

1. **RSA BigInteger leading zero byte** — `BigInteger.toByteArray()` may include a leading `0x00` sign byte. Strip it before reading the magic byte.

2. **ISAAC seed `+50` direction** — The `+50` offset applies to the **server's encoding** cipher and the **client's decoding** cipher. Swapping these will corrupt all post-login packets.

3. **Endianness** — All multi-byte values are **big-endian**. Java's `DataInputStream` and `ByteBuffer` default to big-endian, which is correct.

4. **XTEA block alignment** — XTEA operates on 8-byte blocks. If the XTEA payload is not a multiple of 8 bytes, the last partial block may be handled differently (either padded or left unencrypted — validate against client behaviour).

5. **Client revision check** — Always validate the revision **before** attempting RSA decryption. Saves unnecessary crypto work on stale/bot connections.

6. **Thread safety** — ISAAC cipher instances are stateful and not thread-safe. Each player session must have its own cipher pair.

7. **Password handling** — Never store plaintext passwords. Hash with bcrypt or Argon2 before storage. Compare hash in constant time to avoid timing attacks.

---

*Document generated from analysis of `Aeroverra/runelite` deob fork and RS2/OSRS community protocol research. For questions, contact Avery.*
