# Online detection: what this repository does, and what dice-to-seed does instead

Status: reference, with one open recommendation at the end.

Authored by Pete Sparrow (human) and Claude (AI, Anthropic).

This exists because the two repositories were assumed to work the same way and do
not, and because the difference is not a matter of taste: they answer two
different questions, and only one of them is the question that matters for a
hosted copy.

---

## What this repository does

Two mechanisms, one per host, both behind `IConnectivityProbe` so the UI does not
know which it is talking to.

### The Tauri build: kernel link state

`src-tauri/src/net.rs` exposes `is_online` as a Tauri command. It reads
`/sys/class/net/<interface>/carrier` and reports online when any non-loopback
interface has a live carrier.

Details worth keeping:

- **`carrier`, not `operstate`.** Idle NIC drivers commonly report `operstate`
  as `unknown`, which false-positives an airgapped machine as online.
- **Fail-safe direction is "online".** If `/sys` cannot be enumerated, or the
  enumeration breaks part way through, it reports online. Saying online when the
  machine is airgapped costs the user a watermark they did not deserve. Saying
  offline when the machine is connected hands them a backup that presents itself
  as trustworthy, made from a seed typed on a networked computer.
- **One unreadable `carrier` file is different** from an unreadable directory:
  the kernel refuses that read for an admin-down interface, which means no link
  is possible, so that interface counts as offline.
- Covered by five Rust tests, including the missing-directory case.

This is a good check, and it is a check about **the machine**.

### The web build: outbound requests to third parties

`Slip39Demo.Web/wwwroot/connectivity.js` fetches, with `mode: 'no-cors'`:

    https://www.gstatic.com/generate_204
    https://cloudflare-dns.com/dns-query?name=example.com
    https://www.google.com/generate_204

A response means online. Note what this is: a page about protecting seed phrases
making requests to Google and Cloudflare. It is also the reason `pages.yml` has to
allowlist those hosts in its external-origin check, so the check that exists to
prove the app depends on nothing outside itself carries a permanent exception for
the probe.

## What dice-to-seed does

**No connectivity detection at all.** No probe, no fetch, no carrier read.

Instead, `DiceToSeed.Core/ServingOrigin.cs` asks a different question: was this
copy served to you by somebody else? `IsLocal(uri)` returns true for

- any non-web scheme, so `tauri:` and `file:` are local, which is how the AppImage
  avoids being warned at
- the exact host `localhost`, case-insensitively
- any whole-label suffix `.localhost`, per RFC 6761, which covers Tauri serving
  from `tauri.localhost`
- loopback literals: `127.0.0.0/8` and `::1`, bracketed or bare

Anything else is somebody else's server, and the page shows a loud red banner.

It is a function in Core with tests rather than an expression inline in the page,
and the comment explains why in a sentence worth repeating: *"the Pages workflow
claimed 'that behaviour is tested' when nothing tested it. A safety property
asserted in a comment is not a safety property."* It also rejects
`127.0.0.1.example.com` and `localhost.evil.com`, which a `contains` or
`startsWith` check would wave through.

## The two questions, and why the difference matters

| | asks | can the user change it by unplugging? |
| --- | --- | --- |
| carrier read, remote fetch | is there a network **right now** | yes |
| serving origin | did this code arrive **from a server** | no |

For the AppImage, "is there a network right now" is exactly right: the code came
off a USB stick whose checksum the user verified, so the only remaining question
is the machine.

For a hosted copy it is the wrong question, and dice-to-seed's banner says why
better than a table can: *"Nothing is transmitted, but you cannot verify from here
that the files a server sent you are the ones in the repository."* The risk is not
transmission. It is provenance of the code you are running.

## The gap this leaves here, which is not theoretical

On the hosted demo at `petesparrowbtc.github.io/slip39-backup/`, a visitor can:

1. open the page, see the red ONLINE warning
2. disconnect the network
3. watch `ConnectivityBanner` flip to **"✓ No internet reachable. This machine is
   offline."**
4. reasonably conclude it is now safe to type a real seed

At step 4 they are running JavaScript and WebAssembly that GitHub Pages sent them,
which they cannot verify against the repository, on their everyday computer. The
banner is telling the truth about connectivity and the wrong thing about safety.

The static warning below it does say Tails is required. The dynamic green tick
argues against it, and a green tick beats a paragraph.

## Recommendation, not yet implemented

Adopt the origin check **in addition to** the carrier check, not instead of it:

- Port `ServingOrigin.IsLocal` into `Slip39Demo.Core`, with the tests. It is
  host-agnostic and belongs next to the other pure logic.
- When the origin is not local, show the served-from warning and **suppress the
  green offline tick entirely**. Offline is necessary and not sufficient there, so
  a reassuring state should not be reachable.
- Keep the carrier probe as the gate for the INSECURE-TEST watermark in the Tauri
  build, where it is the right question.
- Then the three third-party probe URLs can go, along with the allowlist exception
  they force on `pages.yml`, since the hosted build would no longer need to ask
  whether there is a network in order to know it is untrustworthy.

That is a behaviour change to a safety-relevant banner, so it belongs in its own
pull request rather than riding along with this note.
