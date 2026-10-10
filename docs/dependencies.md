# Mobile dependency review

The mobile app uses the current `expo` **latest** stable tag, SDK **57.0.27**, and versions selected from that SDK’s compatibility map. SDK 58 is currently tagged `next`; it is not used for this milestone. Web and mobile intentionally maintain separate dependency trees.

`npm audit --omit=dev` reports **28 affected package entries (18 high, 10 moderate)** in the mobile dependency tree. These entries trace back to four underlying advisories; the count includes packages depending on affected packages:

| Package                      | Advisory                                                                                                          | Where it enters the mobile tree           |
| ---------------------------- | ----------------------------------------------------------------------------------------------------------------- | ----------------------------------------- |
| `braces` 3.0.3               | [GHSA-vfj7-8cjw-p6xm](https://github.com/advisories/GHSA-vfj7-8cjw-p6xm): deeply nested pattern denial of service | Metro file matching through `micromatch`  |
| `node-forge` 1.4.0           | [GHSA-86w9-cpqp-85rv](https://github.com/advisories/GHSA-86w9-cpqp-85rv): RSA signature verification              | Expo CLI/code-signing tooling             |
| `decode-uri-component` 0.2.2 | [GHSA-vcc3-ghjq-m6fr](https://github.com/advisories/GHSA-vcc3-ghjq-m6fr): malformed URI denial of service         | Expo Router’s `query-string` dependency   |
| `uuid` 7.0.3                 | [GHSA-w5hq-g745-h8pq](https://github.com/advisories/GHSA-w5hq-g745-h8pq): supplied-buffer bounds checking         | `xcode`/Expo native configuration tooling |

The installed `braces` and `node-forge` versions are their latest stable releases and the audit currently lists all versions as affected. The other fixes cross the declared dependency ranges. For example, the patched URI decoder is an ES module while Expo Router’s current query-string version uses a CommonJS require. A blind override would risk breaking navigation. npm’s proposed forced fixes include downgrading Expo to SDK 44 or moving Expo Router outside SDK 57.

The milestone therefore retains the supported SDK combination, records the findings, and does not claim a clean mobile security audit. Keep Metro on trusted development interfaces. Before distributing native builds, recheck the advisories and adopt a patched, compatible Expo release or validate a focused upstream fix. No native app is signed or published by this repository’s CI.

To repeat the audit:

```bash
cd apps/mobile
npm audit --omit=dev
npm run check:dependencies
```

Mobile tests, type checking, and the Android JavaScript export pass with the current lockfile. These checks establish functionality; they do not erase the dependency findings. The web production dependency audit returned zero known vulnerabilities during milestone validation.
