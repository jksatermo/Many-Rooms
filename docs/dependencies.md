# Mobile dependency review

The mobile app uses the current `expo` **latest** stable tag, SDK **57.0.27**, and versions selected from that SDK’s compatibility map. SDK 58 is currently tagged `next`; it is not used for this milestone. Web and mobile intentionally maintain separate dependency trees.

The Milestone 2 review on October 10, 2026 found **five underlying advisories** in the mobile dependency tree. The latest `npm audit --omit=dev` snapshot reported **60 affected package entries (45 high, 15 moderate)**; the count includes packages depending on affected packages. npm's aggregate counts varied while its cached dependency-advisory metadata refreshed, so the specific advisories below are the more useful record. The Milestone 1 review identified four advisories and 28 affected entries before native component-test dependencies were installed.

| Package                      | Advisory                                                                                                                   | Where it enters the mobile tree                                  |
| ---------------------------- | -------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------- |
| `braces` 3.0.3               | [GHSA-vfj7-8cjw-p6xm](https://github.com/advisories/GHSA-vfj7-8cjw-p6xm): deeply nested pattern denial of service          | Metro file matching through `micromatch`                         |
| `node-forge` 1.4.0           | [GHSA-86w9-cpqp-85rv](https://github.com/advisories/GHSA-86w9-cpqp-85rv): RSA signature verification                       | Expo CLI/code-signing tooling                                    |
| `decode-uri-component` 0.2.2 | [GHSA-vcc3-ghjq-m6fr](https://github.com/advisories/GHSA-vcc3-ghjq-m6fr): malformed URI denial of service                  | Expo Router’s `query-string` dependency                          |
| `uuid` 7.0.3                 | [GHSA-w5hq-g745-h8pq](https://github.com/advisories/GHSA-w5hq-g745-h8pq): supplied-buffer bounds checking                  | `xcode`/Expo native configuration tooling                        |
| `sprintf-js` 1.0.3           | [GHSA-hp3w-g68c-fv3c](https://github.com/advisories/GHSA-hp3w-g68c-fv3c): unbounded formatting precision denial of service | Jest/Istanbul configuration through `js-yaml` 3 and `argparse` 1 |

The installed `braces` and `node-forge` versions are their latest stable releases and the audit currently lists all versions as affected. `sprintf-js` has no published patched stable release either: the registry's latest is 1.1.3, which is affected, and the installed `argparse` range selects 1.0.3. The current compatible upstream chain (`@istanbuljs/load-nyc-config` 1.1.0 → `js-yaml` 3.15.2 → `argparse` 1.0.10) still depends on it. The other fixes cross the declared dependency ranges. For example, the patched URI decoder is an ES module while Expo Router’s current query-string version uses a CommonJS require. A blind override would risk breaking navigation. npm’s proposed forced fixes include downgrading Expo to SDK 44 or moving Expo Router outside SDK 57.

Milestone 2 adds SDK-compatible Jest and React Native Testing Library dependencies for native component interactions. React Native lists its Jest preset as an optional peer, and Expo Router lists Testing Library as an optional peer; npm consequently classifies parts of the installed test tree as `devOptional` and includes them even with `--omit=dev`. `sprintf-js` is reached through test/coverage configuration parsing, not through Many Rooms assignment or availability code. The audit count is not a count of vulnerabilities proven reachable in a shipped native bundle. It also does not establish that a bundle is unaffected; native distribution still requires a separate review. The full development-tree audit also reported these same five advisories.

The milestone therefore retains the supported SDK combination, records the findings, and does not claim a clean mobile security audit. Keep Metro on trusted development interfaces. Before distributing native builds, recheck the advisories and adopt a patched, compatible Expo release or validate a focused upstream fix. No native app is signed or published by this repository’s CI.

To repeat the audit:

```bash
cd apps/mobile
npm audit --omit=dev
npm run check:dependencies
```

Mobile tests, type checking, and the Android JavaScript export pass with the current lockfile. These checks establish functionality; they do not erase the dependency findings. The web production dependency audit returned zero known vulnerabilities during milestone validation.

## Milestone 3 compatibility

Milestone 3 changes no package versions or lockfiles. The existing SDK compatibility check and both Android/Hermes and Expo browser exports still pass. The advisory inventory above is the dated Milestone 2 review, not a claim that a new security audit is clean. Native distribution remains blocked on compatible dependency review and real device validation; no forced upgrades were applied.
