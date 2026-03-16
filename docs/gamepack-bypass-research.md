# Gamepack Host Validation Bypass Research

**Task:** `t_crash_2_research`  
**Author:** Avery (RuneLite Engineer)  
**Date:** 2026-03-16  
**Status:** Complete

---

## Problem Statement

AeroScape client crashes immediately with `error_game_invalidhost`. Patching `runelite.properties` (which points to our custom `jav_config.ws` at `http://51.79.134.188/jav_config.ws`) only controls which config file RuneLite fetches — it does **not** affect the host validation that runs inside the OSRS gamepack itself.

---

## Root Cause: What Is `error_game_invalidhost`?

The `error_game_invalidhost` error originates **inside the obfuscated OSRS gamepack JAR** (historically `gamepack_XXXXXXX.jar` from Jagex, in our case `aeroscape-client.jar`). The gamepack's startup logic does the following:

1. Reads the `codebase` parameter from the AppletStub (`RSAppletStub.getCodeBase()`)
2. Validates that the hostname in `codebase` belongs to an approved Jagex domain (e.g., `*.runescape.com`, `*.jagex.com`, or specific world IPs owned by Jagex)
3. If validation fails, calls `AppletContext.showDocument(new URL("/error_game_invalidhost.ws"))`
4. RuneLite's `RSAppletStub.showDocument()` catches this and displays the fatal error dialog

Our `jav_config.ws` has:
```
codebase=http://51.79.134.185/
```

This is our VPS IP — not a Jagex domain — so the gamepack rejects it immediately.

### How RSAppletStub Handles It

From `runelite-client/src/main/java/net/runelite/client/rs/RSAppletStub.java`:

```java
@Override
public void showDocument(URL url)
{
    if (url.getPath().startsWith("/error_game_"))
    {
        // ... strips path and shows FatalErrorDialog
        // "OldSchool RuneScape has crashed with the message: error_game_invalidhost"
    }
}
```

The gamepack calls into RuneLite via the AppletStub interface — RuneLite just renders the error. The check is entirely in the gamepack bytecode.

---

## What the Gamepack Validates

The gamepack performs an **internal allowlist check** against `getCodeBase().getHost()`. Based on known reverse engineering of OSRS gamepacks, it checks for:

- Hosts ending in `.runescape.com`
- Hosts ending in `.jagex.com`  
- Specific Jagex-owned IP ranges (used for world server addresses)

A raw IP like `51.79.134.185` or `127.0.0.1` will fail this check unless the gamepack is patched.

**Note:** This is NOT an SSL certificate check — it's a plain string/IP hostname comparison in the gamepack's initialization code.

---

## How Other Private Server Clients Bypass It

### Approach 1: OpenOSRS Bytecode Injection (Most Robust)

OpenOSRS (`github.com/open-osrs/runelite`) maintains a dedicated `injector/` module that uses ASM bytecode manipulation to patch the gamepack at runtime before loading it. Their pipeline:

1. Download vanilla gamepack JAR
2. Run it through the `injector` which reads the jar, rewrites bytecode, and produces a patched JAR
3. Load the patched JAR instead

The host validation check is one of the things patched out. Their `RSBufferMixin` (in `runelite-mixins/`) also replaces RSA encryption so logins work with private server RSA keys:

```java
@Mixin(RSBuffer.class)
public abstract class RSBufferMixin implements RSBuffer
{
    @Shadow("modulus")
    private static BigInteger modulus;

    @Copy("encryptRsa")
    @Replace("encryptRsa")
    public void copy$encryptRsa(BigInteger exp, BigInteger mod)
    {
        if (modulus != null)
        {
            mod = modulus;  // substitute private server modulus
        }
        copy$encryptRsa(exp, mod);
    }
}
```

### Approach 2: Custom `jav_config.ws` with `codebase=http://127.0.0.1/`

Projects like `CalvoG/Runelite-RSPS` and `AlterRSPS/Runelite` set:
```
codebase=http://127.0.0.1/
```
Combined with a local HTTP server/proxy that serves assets and relays game traffic to the real server. However, `127.0.0.1` is still a non-Jagex host, so this **alone** does not bypass the gamepack check — the bytecode injection is still required alongside this.

### Approach 3: DNS Spoofing / `/etc/hosts` Trick

Map a Jagex-looking domain to the actual server IP in the OS hosts file:
```
51.79.134.185  world1.runescape.com
```
Then set `codebase=http://world1.runescape.com/` in the jav_config. The gamepack sees a valid Jagex domain name and passes validation. The OS DNS resolves it to our server.

**Pros:** No bytecode patching needed  
**Cons:** Requires client-side OS configuration per player — not distributable, not practical for end users

### Approach 4: Use a Pre-Patched / Custom Gamepack

Some RSPS projects ship a completely custom gamepack (not Jagex's obfuscated client at all). This sidesteps the issue entirely but requires a parallel client codebase and loses RuneLite injection compatibility.

---

## Current AeroScape State

From `aeroscape-client/runelite-client/src/main/resources/net/runelite/client/runelite.properties`:
```
runelite.jav_config=http://51.79.134.188/jav_config.ws
runelite.jav_config_backup=http://51.79.134.188/jav_config.ws
runelite.insecure-skip-tls-verification=true
```

From `aeroscape-server/static/jav_config.ws`:
```
codebase=http://51.79.134.185/
initial_jar=aeroscape-client.jar
initial_class=client.class
```

**The problem:** `codebase` is a raw IP. The gamepack validates this and fires `error_game_invalidhost`. The jav_config is being served and parsed correctly — the issue is purely in the gamepack's startup validation.

It's unclear whether `aeroscape-client.jar` is the original Jagex gamepack (renamed) or has been through OpenOSRS-style injection. If it's unpatched vanilla, the host check is still live. If it was injected but the host check wasn't patched, same result.

---

## Recommended Approach for AeroScape

### Primary Recommendation: ASM Bytecode Patch on the Gamepack

**Estimated effort: 2–4 hours**

1. **Decompile the gamepack** using `fernflower` or `procyon` to locate the host validation code:
   ```bash
   java -jar fernflower.jar aeroscape-client.jar /tmp/decompiled/
   grep -r "invalidhost\|runescape.com\|jagex.com\|getHost" /tmp/decompiled/
   ```

2. **Write an ASM transformer** (or use a pre-built one from OpenOSRS's injector) that:
   - Finds the method containing the host allowlist check
   - Either NOPs out the conditional branch or replaces the check with `return true`

3. **Integrate into `ClientLoader.java`**: After downloading/loading the gamepack JAR, run it through the patcher before injecting it:
   ```java
   // In ClientLoader.loadClient() or updateVanilla()
   byte[] patchedBytes = GamepackHostPatcher.patch(rawJarBytes);
   // Load patchedBytes instead of rawJarBytes
   ```

4. **Ship the patched JAR** or patch at load time (the latter is cleaner — no storing patched binaries).

### Secondary Recommendation: DNS-Based Spoofing (Quick Win for Testing)

For immediate unblocking during development:

1. On the dev machine, add to `/etc/hosts`:
   ```
   51.79.134.185  world1.runescape.com
   ```
2. Change `jav_config.ws` `codebase` to `http://world1.runescape.com/`
3. Test locally to confirm this unblocks `error_game_invalidhost`

This confirms the root cause and unblocks testing **without** requiring bytecode work. Production release still needs the ASM patch.

### Quick-Win Option: Patch codebase Host at RSAppletStub Level

A lower-effort workaround is to hardcode the codebase URL in `RSAppletStub.getCodeBase()` to return a Jagex-looking URL while the actual game traffic goes to our server. But this won't work because the gamepack also validates the server it connects to for game protocol — not just the codebase URL — so RSA key replacement is also needed.

---

## Required Changes Summary

| Component | Change | Effort |
|-----------|--------|--------|
| `aeroscape-client.jar` | ASM patch to remove host allowlist check | 2–4 hrs |
| `runelite-mixins` | RSBufferMixin RSA key replacement (per OpenOSRS pattern) | 1–2 hrs |
| `jav_config.ws` | Keep `codebase` as-is (IP is fine post-patch) | 0 |
| `runelite.properties` | No change needed | 0 |

**Total estimated effort:** 3–6 hours for a clean implementation.

---

## References

- `runelite-client/src/main/java/net/runelite/client/rs/RSAppletStub.java` — Shows how `error_game_*` errors bubble up from gamepack
- `runelite-client/src/main/java/net/runelite/client/rs/ClientLoader.java` — Gamepack loading pipeline
- `github.com/open-osrs/runelite` — injector module, RSBufferMixin (RSA bypass pattern)
- `github.com/NateChambers/RuneLitePlus-PrivateServerEdition` — RSAppletStub codebase override approach
- `github.com/CalvoG/Runelite-RSPS` — jav_config.ws with `codebase=http://127.0.0.1/`
