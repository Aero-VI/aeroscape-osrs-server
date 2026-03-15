# AeroScape Phase 1 — QA Automation Plan

**Author:** Nova (Head of Quality, Aeroverra)  
**Date:** 2026-03-15  
**Version:** 1.0  
**Mission:** AeroScape Phase 1 — Login Proof of Concept

---

## 1. Test Strategy

### What We're Testing

Phase 1 has one job: **a player can launch the client, enter credentials, and the server accepts the login.** QA must validate every link in that chain.

| Layer | What to Test | Why |
|---|---|---|
| **Client Launch** | JAR starts without crashing | No point testing a dead client |
| **Client → Server Connection** | TCP connection established to `51.79.134.188:43594` | Network path must be open |
| **Login Handshake** | OSRS protocol handshake completes (RSA + ISAAC) | Core protocol correctness |
| **Server Acceptance** | Server sends response code `2` (success) | Phase 1 acceptance criterion |
| **Server Logging** | Login event appears in server logs | Observability for ops |
| **Rejection Path** | Server still returns `2` for invalid/garbage credentials | Intentional permissive-auth behavior |
| **Error Path** | Client handles connection refused gracefully | Stability under adverse conditions |
| **Concurrency** | Server handles multiple simultaneous logins | Basic stress for a TCP server |

### Why Automate?

Manual testing is a one-shot snapshot. Automated tests catch regressions as the server evolves. Given that:
- The login protocol is byte-level precise (wrong byte = silent failure)
- We will be adding game world features in future phases
- The team is small and manual regression is expensive

Automation pays off starting from Phase 1.

---

## 2. Technology Evaluation

### Option A: Java Robot / AWT Headless

**What it is:** Java's `java.awt.Robot` can inject keyboard/mouse events into a running GUI. With a headless display (Xvfb on Linux), you can drive the RuneLite UI programmatically.

**Pros:**
- Same JVM as the client — no external process overhead
- Can interact with Swing/AWT components directly if you can get a reference to them
- No image recognition needed for simple text fields

**Cons:**
- RuneLite renders via LWJGL/OpenGL onto a canvas — it is NOT a standard Swing form. AWT component references won't reach the game canvas.
- Requires a virtual display (Xvfb) in CI — adds infra complexity
- Fragile to window position changes
- Cannot query internal game state without source access

**Verdict:** ❌ Not recommended. RuneLite's canvas is GPU-rendered; AWT component introspection won't work on the login form.

---

### Option B: SikuliX Image Automation

**What it is:** SikuliX uses OpenCV-based image pattern matching to find UI elements on screen and click/type at them.

**Pros:**
- Works on any GUI, regardless of how it's rendered
- Can verify visual state (e.g. "login button is visible")
- Relatively easy to author test scripts

**Cons:**
- Requires a real or virtual display with proper GPU/rendering — RuneLite needs OpenGL
- Image matching is resolution/DPI-sensitive — brittle in CI
- Slow: each match requires screen capture + template search
- Requires pre-captured reference images for every UI element
- RuneLite has a loading screen with a progress bar that must complete before the login form appears — timing is nondeterministic

**Verdict:** ⚠️ Possible but fragile. Viable only if a stable virtual display with OpenGL passthrough is available. Not recommended for a first implementation.

---

### Option C: Playwright Desktop

**What it is:** Playwright is a browser automation framework. It has experimental support for Electron apps but does **not** support arbitrary Java/OpenGL desktop applications like RuneLite.

**Pros:**
- Excellent for web frontends
- Good CI/CD integration

**Cons:**
- RuneLite is not an Electron app, not a web app, and not a supported Playwright target
- Would require a headless browser proxy layer that doesn't exist for this use case

**Verdict:** ❌ Not applicable. Wrong tool for this target.

---

### Option D: RuneLite Test Client Fork

**What it is:** Modify the RuneLite source to add a "headless test mode" — a code path that runs the login sequence programmatically without rendering the UI.

**Pros:**
- Complete control — you can bypass the UI entirely
- Can verify internal state (connected, login response code received)
- No fragile image matching; pure code assertions
- Runs in CI without any display

**Cons:**
- Requires non-trivial source changes to RuneLite's client initialization
- Changes must be maintained as the codebase evolves
- Harder to implement correctly — needs deep RuneLite knowledge (Avery's domain)
- Risk of test code diverging from real client behavior

**Verdict:** ✅ Strong option for future phases. For Phase 1, the implementation cost is high for what is essentially a TCP test.

---

### Option E: Direct TCP Socket Test (Bypass Client)

**What it is:** A standalone test program (Java or Python) that connects to `51.79.134.188:43594` and manually sends a valid OSRS login packet, then verifies the server responds with code `2`.

**Pros:**
- No display required — runs anywhere, including CI
- Tests the server protocol directly — if this fails, the client will too
- Fast (milliseconds per test)
- Easy to implement — just socket code + byte crafting
- Can be written in Java (matching the protocol research) or Python (easier to author)
- Completely reliable — no UI state machines, no image matching

**Cons:**
- Does NOT test the client JAR itself — a bug in client-side packet construction won't be caught
- Tests the server, not the full end-to-end user flow

**Verdict:** ✅ **Best option for Phase 1.** Fast, reliable, CI-friendly.

---

## 3. Recommended Approach

### Primary: Direct TCP Socket Tests

**Recommendation: Implement a Java test suite using JUnit 5 that directly speaks the OSRS login protocol via TCP sockets.**

**Rationale:**

Phase 1's acceptance criterion is: *"server handles a login."* The most direct, reliable way to test this is to send it a login packet and check the response. Socket tests:

1. Run in CI with zero display infrastructure
2. Execute in milliseconds — no waiting for GPU to render a loading screen
3. Are deterministic — same bytes in, same bytes out
4. Cover the server's primary responsibility (protocol correctness)
5. Can be expanded incrementally as the protocol evolves

Client-side GUI automation (SikuliX, AWT Robot) adds enormous complexity for a Phase 1 proof of concept. If the server protocol works correctly, and the client is a known-good RuneLite fork with minimal changes (just the IP swap), the risk of client-side login logic being broken is low. Manual verification by Nicholas serves as the end-to-end smoke test.

**Phase 2+ Enhancement:** Once Phase 1 is stable, implement a headless test fork of the client (Option D) to catch client-side regressions as game features are added.

---

## 4. Test Scenarios

### TC-001: Valid Login Flow
**Description:** Connect to the server and send a syntactically correct OSRS login packet with valid-format credentials.  
**Input:** Username `testuser`, Password `testpass`  
**Expected:** Server responds with byte `2` (LOGIN_RESPONSE_OK)  
**Priority:** P0 — Must pass for Phase 1 sign-off

---

### TC-002: Invalid Credentials (Server Must Accept)
**Description:** Connect and send credentials that would fail on a real OSRS server (wrong password, account doesn't exist, etc.).  
**Input:** Username `wronguser`, Password `wrongpassword`  
**Expected:** Server still responds with byte `2` — permissive auth is intentional in Phase 1  
**Priority:** P0 — Validates the "accept anything" design decision

---

### TC-003: Blank/Empty Credentials
**Description:** Send a login packet with empty username and/or empty password strings.  
**Input:** Username `""`, Password `""`  
**Expected:** Server responds with `2` OR with a defined error code (not a crash/disconnect)  
**Priority:** P1 — Server must not crash on edge-case input

---

### TC-004: Oversized Credential Strings
**Description:** Send username/password strings at or beyond typical OSRS limits (username > 12 chars, password > 20 chars).  
**Input:** Username `aaaaaaaaaaaaaaaaaaaaaa` (22 chars), Password `bbbbbbbbbbbbbbbbbbbbbb` (22 chars)  
**Expected:** Server handles gracefully — responds or cleanly disconnects, no crash  
**Priority:** P1 — Related to the uncapped payloadSize finding in the security review

---

### TC-005: Malformed Packet (Invalid Opcode)
**Description:** Connect and send garbage bytes that don't match the OSRS login opcode.  
**Input:** `[0xFF, 0x00, 0x00, ...]`  
**Expected:** Server disconnects cleanly, does not crash, continues accepting other connections  
**Priority:** P1 — Validates Cipher's security review finding on exception handling

---

### TC-006: Connection Refused Handling
**Description:** Attempt to connect to a port where nothing is listening.  
**Input:** Connect to `51.79.134.188:9999` (wrong port)  
**Expected:** Test receives `ConnectionRefusedException` within 5 seconds — documents expected client behavior  
**Priority:** P2 — Documents failure mode for client error handling work

---

### TC-007: Multi-Client Stress Test
**Description:** Open 10 simultaneous TCP connections and send login packets concurrently.  
**Input:** 10 threads, each sending TC-001 payload  
**Expected:** All 10 receive response `2`, no connections are silently dropped, server remains responsive afterward  
**Priority:** P2 — Basic concurrency validation

---

### TC-008: Server Log Verification
**Description:** After TC-001, check that the server stdout/log contains an entry for the login event.  
**Input:** Same as TC-001, plus log scraping  
**Expected:** Log line containing the username `testuser` appears within 2 seconds of the login  
**Priority:** P1 — Validates observability (required for ops)  
**Note:** Requires log access — implement via SSH or a dedicated health endpoint

---

## 5. Implementation Outline

### Step 1: Add Test Project to Solution

```bash
cd aeroscape-server/
# Create a Java test module (standalone, not in the C# solution)
mkdir -p tests/qa-tcp
cd tests/qa-tcp
# Initialize Maven project
mvn archetype:generate -DgroupId=com.aeroverra.aeroscape.qa \
  -DartifactId=aeroscape-tcp-tests \
  -DarchetypeArtifactId=maven-archetype-quickstart \
  -DarchetypeVersion=1.4
```

Or alternatively, create a standalone Java Gradle project:

```
tests/
  qa-tcp/
    build.gradle
    src/test/java/com/aeroverra/aeroscape/qa/
      LoginProtocolTest.java
      StressTest.java
      EdgeCaseTest.java
    src/main/java/com/aeroverra/aeroscape/qa/
      OsrsLoginPacket.java    ← packet builder
      LoginTestClient.java    ← TCP client wrapper
```

---

### Step 2: Implement the OSRS Login Packet Builder

Based on `LOGIN_PROTOCOL.md`, implement `OsrsLoginPacket.java`:

```java
public class OsrsLoginPacket {
    // Builds a minimal valid OSRS login request byte array
    // Fields: opcode, payload length, revision, ISAAC seed,
    //         RSA-encrypted block (username + password + UID)
    public static byte[] build(String username, String password) { ... }
}
```

Reference `LOGIN_PROTOCOL.md` in this repo for exact byte layout. Use the same RSA public key exponent/modulus that the server expects (from its `privateKey.xml`).

---

### Step 3: Implement LoginTestClient

```java
public class LoginTestClient {
    private final String host;
    private final int port;

    public int sendLogin(String username, String password) throws Exception {
        try (Socket socket = new Socket(host, port)) {
            socket.setSoTimeout(5000);
            OutputStream out = socket.getOutputStream();
            InputStream in = socket.getInputStream();
            
            // Step 1: Send connection init (opcode 14)
            out.write(new byte[]{14, 0}); // placeholder — see LOGIN_PROTOCOL.md
            out.flush();
            
            // Step 2: Read server challenge (8-byte ISAAC seed)
            byte[] challenge = in.readNBytes(8);
            
            // Step 3: Send login block
            byte[] loginPacket = OsrsLoginPacket.build(username, password, challenge);
            out.write(loginPacket);
            out.flush();
            
            // Step 4: Read response code
            return in.read(); // expect 2
        }
    }
}
```

---

### Step 4: Write JUnit 5 Tests

```java
@TestInstance(TestInstance.Lifecycle.PER_CLASS)
public class LoginProtocolTest {
    
    static final String HOST = System.getenv().getOrDefault("AEROSCAPE_HOST", "51.79.134.188");
    static final int PORT = Integer.parseInt(System.getenv().getOrDefault("AEROSCAPE_PORT", "43594"));
    
    LoginTestClient client;
    
    @BeforeAll
    void setup() {
        client = new LoginTestClient(HOST, PORT);
    }
    
    @Test
    @DisplayName("TC-001: Valid credentials → response code 2")
    void validLoginReturnsSuccess() throws Exception {
        int response = client.sendLogin("testuser", "testpass");
        assertEquals(2, response, "Server should return LOGIN_RESPONSE_OK (2)");
    }
    
    @Test
    @DisplayName("TC-002: Invalid credentials → still response code 2")
    void invalidCredentialsAccepted() throws Exception {
        int response = client.sendLogin("wronguser", "wrongpass");
        assertEquals(2, response, "Server accepts any credentials in Phase 1");
    }
    
    @Test
    @DisplayName("TC-003: Empty credentials → no crash")
    void emptyCredentialsHandled() throws Exception {
        assertDoesNotThrow(() -> client.sendLogin("", ""));
    }
    
    @Test
    @DisplayName("TC-005: Malformed packet → server stays alive")
    void malformedPacketDoesNotCrashServer() throws Exception {
        // Send garbage
        try (Socket s = new Socket(HOST, PORT)) {
            s.setSoTimeout(3000);
            s.getOutputStream().write(new byte[]{(byte)0xFF, 0x00, 0x00});
            s.getOutputStream().flush();
        }
        // Server should still respond to a valid login after this
        int response = client.sendLogin("testuser", "testpass");
        assertEquals(2, response, "Server must survive a malformed packet");
    }
}
```

---

### Step 5: Stress Test

```java
@Test
@DisplayName("TC-007: 10 concurrent logins all succeed")
void concurrentLoginsAllSucceed() throws Exception {
    int threads = 10;
    ExecutorService pool = Executors.newFixedThreadPool(threads);
    List<Future<Integer>> futures = new ArrayList<>();
    
    for (int i = 0; i < threads; i++) {
        final int id = i;
        futures.add(pool.submit(() -> client.sendLogin("user" + id, "pass" + id)));
    }
    
    pool.shutdown();
    pool.awaitTermination(30, TimeUnit.SECONDS);
    
    for (Future<Integer> f : futures) {
        assertEquals(2, f.get().intValue(), "All concurrent logins must succeed");
    }
}
```

---

### Step 6: CI Integration

Add to `.github/workflows/qa.yml`:

```yaml
name: QA — TCP Login Tests

on:
  push:
    branches: [main]
  pull_request:

jobs:
  tcp-login-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-java@v4
        with:
          java-version: '17'
          distribution: 'temurin'
      - name: Run QA TCP Tests
        working-directory: tests/qa-tcp
        env:
          AEROSCAPE_HOST: ${{ secrets.AEROSCAPE_HOST }}
          AEROSCAPE_PORT: ${{ secrets.AEROSCAPE_PORT }}
        run: ./gradlew test
      - name: Publish Test Report
        uses: mikepenz/action-junit-report@v4
        if: always()
        with:
          report_paths: 'tests/qa-tcp/build/test-results/**/*.xml'
```

**Note:** The server must be running and accessible from the CI runner. Options:
1. Self-hosted GitHub Actions runner on the same network as VM 105
2. GitHub Actions + Tailscale exit node to reach the private server
3. Run the server in a Docker container as a CI service (future phase)

---

### Step 7: Local Dev Run

```bash
cd tests/qa-tcp
AEROSCAPE_HOST=51.79.134.188 AEROSCAPE_PORT=43594 ./gradlew test
# or for a quick smoke test:
./gradlew test --tests "*.LoginProtocolTest.validLoginReturnsSuccess"
```

---

## 6. Acceptance Criteria

**Phase 1 QA PASS requires all of the following:**

| # | Criterion | Must Pass |
|---|---|---|
| 1 | TC-001 passes: valid login → response code `2` | ✅ Required |
| 2 | TC-002 passes: invalid credentials → response code `2` | ✅ Required |
| 3 | TC-005 passes: malformed packet does not crash server | ✅ Required |
| 4 | TC-007 passes: 10 concurrent logins all return `2` | ✅ Required |
| 5 | Server remains responsive after all tests complete | ✅ Required |
| 6 | No unhandled exceptions in server logs during test run | ✅ Required |
| 7 | Nicholas can run the JAR manually and reach a login screen | ✅ Required (manual) |

**Phase 1 QA STRETCH (nice-to-have, not blocking):**

| # | Criterion |
|---|---|
| S1 | TC-008: Server log verification automated |
| S2 | GitHub Actions CI runs on every push to `main` |
| S3 | SikuliX smoke test: client JAR launches and login screen renders |

---

## 7. Future Phase Roadmap

| Phase | QA Addition |
|---|---|
| **Phase 2** (game world) | Headless RuneLite test fork (Option D) — verifies player loads into world |
| **Phase 2** | SikuliX integration for full visual regression |
| **Phase 3** (multi-player) | Load test with 50+ concurrent clients |
| **Phase 3** | Automated regression suite on every PR |
| **Future** | Dedicated QA VM in Proxmox for isolated test runs |

---

## Appendix: Reference Documents

- [`LOGIN_PROTOCOL.md`](../LOGIN_PROTOCOL.md) — OSRS handshake protocol reference (by Avery)
- [`SECURITY_REVIEW.md`](../SECURITY_REVIEW.md) — Security findings from Cipher's review
- [OSRS Login Protocol — RuneScape Wiki](https://wiki.vg/RuneScape_Login_Protocol) *(external reference)*
- [JUnit 5 User Guide](https://junit.org/junit5/docs/current/user-guide/)
- [SikuliX Documentation](https://sikulix-2014.readthedocs.io/en/latest/)

---

*Nova — Head of Quality, Aeroverra*  
*AeroScape Phase 1 QA Automation Plan v1.0*
