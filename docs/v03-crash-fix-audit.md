# Aeroscape Client Audit: v0.3 Crash Fix — RSAppletStub & WorldService Findings

**Auditor:** Avery (RuneLite Engineer, Aeroverra)  
**Date:** 2026-03-16  
**Repo:** Aero-VI/aeroscape-client  
**Purpose:** Root-cause `error_game_invalidhost` and document every file that needs changing.

---

## 1. Executive Summary

The `error_game_invalidhost` error is thrown by the OSRS game engine (injected-client) when the host it is told to connect to does not match the `codebase` value provided by the applet stub. The Aeroscape client currently reads the codebase from `jav_config.ws` and passes it through without validation or override. If the private server's `jav_config.ws` emits a codebase of `http://51.79.134.188/` but the game engine's internal host allowlist rejects that IP (or validates against a Jagex domain), the engine calls `AppletStub.showDocument("/error_game_invalidhost.ws")`. The client's `RSAppletStub.showDocument()` only handles three specific error codes — `error_game_js5connect`, `error_game_js5io`, and `error_game_crash` — and falls through to a generic fatal dialog for everything else, including `error_game_invalidhost`.

---

## 2. RSAppletStub — Current `getCodeBase()` Implementation

**File:** `runelite-client/src/main/java/net/runelite/client/rs/RSAppletStub.java`

```java
@Override
public URL getCodeBase()
{
    try
    {
        return new URL(config.getCodeBase());
    }
    catch (MalformedURLException ex)
    {
        return null;
    }
}
```

**What `config.getCodeBase()` returns:**  
`RSConfig.getCodeBase()` reads `classLoaderProperties.get("codebase")` — which is populated by `ClientConfigLoader` by parsing the `codebase=` line from `jav_config.ws`.

The private server's `jav_config.ws` is served from `http://51.79.134.188/jav_config.ws` (confirmed in `runelite.properties`). Whatever `codebase=` is set to in that file becomes the URL returned by `getCodeBase()`. This URL is passed directly to the OSRS game engine as its applet stub.

**The problem:** The vanilla OSRS game engine validates the codebase host against Jagex-controlled domains (e.g., `*.runescape.com`, `*.jagex.com`). When the codebase is a bare IP address (`http://51.79.134.188/`) the engine rejects it and calls `showDocument("/error_game_invalidhost.ws")`.

---

## 3. How the Codebase URL Flows from `jav_config.ws` into the Applet Stub

```
jav_config.ws (served from http://51.79.134.188/jav_config.ws)
    │
    ▼ fetched by
ClientConfigLoader.fetch(url)          [ClientConfigLoader.java]
    │  parses key=value lines; "codebase=..." → classLoaderProperties["codebase"]
    ▼
RSConfig.getCodeBase()                 [RSConfig.java]
    │  returns classLoaderProperties.get("codebase")
    ▼
RSAppletStub.getCodeBase()             [RSAppletStub.java]
    │  wraps in new URL(...)
    ▼
OSRS game engine (injected-client)
    │  calls getCodeBase() to validate host before connecting
    │  if host is not on Jagex allowlist → showDocument("/error_game_invalidhost.ws")
    ▼
RSAppletStub.showDocument()            [RSAppletStub.java]
    └─ falls through to generic FatalErrorDialog (no specific handler for invalidhost)
```

---

## 4. World List URL and Overridability

**File:** `runelite-client/src/main/java/net/runelite/client/game/WorldClient.java`

```java
HttpUrl url = apiBase.newBuilder()
    .addPathSegment("worlds.js")
    .build();
```

`apiBase` is injected as `@Named("runelite.api.base")` and resolved in `RuneLiteModule`:

```java
@Provides @Named("runelite.api.base")
HttpUrl provideApiBase(@Named("runelite.api.base") String s) {
    final String prop = System.getProperty("runelite.http-service.url");
    return HttpUrl.get(Strings.isNullOrEmpty(prop) ? s : prop);
}
```

The default value comes from `runelite.properties`:

```
runelite.api.base=https://api.runelite.net/runelite-${project.version}
```

So the world list URL is currently: `https://api.runelite.net/runelite-<version>/worlds.js`

**Is it overridable?** Yes — via JVM system property `runelite.http-service.url`. No code changes needed to redirect world list fetches to the private server's API, just a launch arg. However, for an RSPS build, this should be hardcoded or configured properly.

**WorldService** (`WorldService.java`) uses `WorldClient` (injected via constructor) which uses this same `apiBase`. `WorldService` is used by the world switcher plugin. `WorldSupplier` (used during initial boot/failover in `ClientLoader`) also uses `WorldClient` with the same `apiBase`.

---

## 5. Exact List of Files That Need Changes

| File | Location | Why It Needs Changing |
|------|----------|----------------------|
| `RSAppletStub.java` | `runelite-client/src/main/java/net/runelite/client/rs/` | Must intercept/suppress `error_game_invalidhost` or override `getCodeBase()` to return an accepted URL |
| `ClientLoader.java` | `runelite-client/src/main/java/net/runelite/client/rs/` | `downloadConfig()` retry logic uses `WorldSupplier` (Jagex world list); fallback config path also hits `WorldSupplier`. For RSPS, failover should use private server worlds only |
| `WorldSupplier.java` | `runelite-client/src/main/java/net/runelite/client/rs/` | Hardcodes `RuneLiteProperties.getApiBase()` for world fetching; fallback hardcodes `51.79.134.188` (already correct for RSPS but list fetch URL still goes to RuneLite API) |
| `runelite.properties` | `runelite-client/src/main/resources/net/runelite/client/` | `runelite.api.base` points to RuneLite API; needs to point to private server's API for world list |
| `RuneLiteModule.java` | `runelite-client/src/main/java/net/runelite/client/` | `provideApiBase` — may need to wire a separate RSPS-specific binding for the world list URL separate from other API calls |

---

## 6. Recommended Patch Approach Per File

### 6.1 `RSAppletStub.java` — PRIMARY FIX

**Problem:** `getCodeBase()` returns the raw codebase from `jav_config.ws`, which is an IP that the OSRS engine rejects. Additionally, `showDocument()` has no handler for `error_game_invalidhost`.

**Fix A (Suppress in `showDocument`):** Add an explicit handler for `error_game_invalidhost` so the error doesn't surface as a fatal dialog. This is a minimal band-aid — it hides the symptom but the game may still refuse to load.

**Fix B (Override `getCodeBase()`):** Override the returned URL to be something the game engine accepts. The vanilla OSRS client does host validation against `*.jagex.com` or `*.runescape.com`. For an RSPS that ships a patched (deobfuscated) game jar, this validation can be removed from the injected client instead. However if using the unmodified OSRS jar, you need to satisfy the check.

**Recommended approach:** Since this is an RSPS with a custom game server, the injected-client jar is presumably patched. The patch should remove or bypass the host validation inside the game engine. If not yet patched, add a handler in `showDocument` to suppress `error_game_invalidhost` and **additionally** investigate whether the private server's `jav_config.ws` `codebase=` value needs to match what the engine expects.

```java
// In showDocument(), add before the generic else:
else if (code.equals("error_game_invalidhost"))
{
    // RSPS: host validation disabled - suppress this error
    log.warn("error_game_invalidhost suppressed (RSPS codebase: {})", config.getCodeBase());
    return;
}
```

### 6.2 `ClientLoader.java` — SECONDARY FIX

**Problem:** The retry loop in `downloadConfig()` on failure tries different worlds from `WorldSupplier` (which fetches from RuneLite's API). For RSPS there is only one server (`51.79.134.188`). The fallback config path (`downloadFallbackConfig()`) hits `RuneLiteProperties.getJavConfigBackup()` — currently also `http://51.79.134.188/jav_config.ws`, so this is already correct.

**Fix:** The retry loop should not cycle through RuneLite worlds (it's already prevented by the check `!javConfigUrl.equals(RuneLiteProperties.getJavConfig())` which throws immediately for custom jav_config URLs). Verify this path works correctly for RSPS.

No code changes strictly required here if the private server's `jav_config.ws` is serving correctly.

### 6.3 `WorldSupplier.java` — SECONDARY FIX

**Problem:** Fetches world list from `RuneLiteProperties.getApiBase()` (RuneLite's API). On failure, falls back to `51.79.134.188` hardcoded — this is actually correct for RSPS!

**Fix:** Override `getApiBase()` to point at the private server's API, OR the hardcoded fallback will always fire (since RuneLite's API won't have RSPS worlds). Either approach is acceptable. The simplest fix: point `runelite.api.base` in `runelite.properties` to the private server.

### 6.4 `runelite.properties` — CONFIG FIX

**Current:**
```
runelite.api.base=https://api.runelite.net/runelite-${project.version}
runelite.jav_config=http://51.79.134.188/jav_config.ws
runelite.jav_config_backup=http://51.79.134.188/jav_config.ws
```

**Fix:** Change `runelite.api.base` to point at the private server's REST API (wherever it exposes `worlds.js`). Example:
```
runelite.api.base=http://51.79.134.188/api
```
The server at `51.79.134.188` must then serve `GET /api/worlds.js` in the RuneLite world JSON format.

### 6.5 `WorldService.java` — INFORMATIONAL (no code change needed)

`WorldService` uses the injected `apiBase` from `RuneLiteModule`. Fixing `runelite.properties` or the `RuneLiteModule` binding automatically fixes `WorldService`. No direct changes to `WorldService.java` needed.

---

## 7. Root Cause Conclusion

The `error_game_invalidhost` error originates inside the OSRS game engine (injected-client jar). The engine performs host validation of `getCodeBase()` against Jagex-approved domains. Since the private server uses a bare IP (`51.79.134.188`), the validation fails.

**The fix requires one or more of:**

1. **Patch the injected-client jar** to remove host validation (cleanest solution for RSPS — this is done at the gamepack level, not in RuneLite client code)
2. **Override `getCodeBase()`** in `RSAppletStub` to return a domain that passes validation (e.g., configure DNS to point a jagex-like domain to `51.79.134.188` and return that domain)
3. **Suppress the error** in `RSAppletStub.showDocument()` so at least the dialog doesn't appear (symptom suppression only)

The most robust solution for a self-hosted RSPS is **option 1**: patch the deobfuscated game client to strip the host validation. This is consistent with how most RSPS RuneLite forks operate.

---

## 8. Summary Table

| Component | Current Behavior | Needed Change |
|-----------|-----------------|---------------|
| `RSAppletStub.getCodeBase()` | Returns raw codebase from jav_config (`http://51.79.134.188/`) | N/A if gamepack is patched; OR return domain-based URL |
| `RSAppletStub.showDocument()` | No handler for `error_game_invalidhost` → generic fatal dialog | Add suppression handler |
| `ClientLoader.downloadConfig()` | Retries with RuneLite worlds on failure | Already short-circuits for custom jav_config URL |
| `WorldSupplier` world list URL | `https://api.runelite.net/.../worlds.js` | Point to private server API |
| `WorldService` world list URL | Same as above (uses injected apiBase) | Fixed automatically by updating `runelite.api.base` |
| `runelite.properties` `runelite.api.base` | RuneLite API | Private server API endpoint |
| Injected-client (gamepack jar) | Validates codebase host vs Jagex allowlist | **PATCH REQUIRED** — strip host validation |
