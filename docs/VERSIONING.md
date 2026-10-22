# Versioning

OmniKit uses `major.minor.patch.commitCount`. The base version is `0.1.0`.
The final component is the number of commits reachable from the artifact's
source commit, including that commit. It is not a build counter: rebuilding
the same source commit does not increase it.

The count is padded to at least four digits in release labels. For example,
count 3 becomes `0003`, while count 10000 remains `10000`. The short source
hash follows the version in the display label.

The following values illustrate the format; they do not identify a published
release or the next source commit:

| Use | Example |
| --- | --- |
| Base version | `0.1.0` |
| Release version | `0.1.0.0003` |
| Display label | `0.1.0.0003 (abc1234)` |
| GitHub release tag | `v0.1.0.0003` |
| .NET assembly/file and Dalamud manifest version | `0.1.0.3` |
| Core release asset | `OmniKit-0.1.0.0003-abc1234.zip` |
| Umbra release asset | `OmniKit.Umbra-0.1.0.0003-abc1234.dll` |

The padded and numeric forms represent the same components. Padding is only
presentation; compare versions numerically. The release tag identifies the full
source commit. The short hash is a convenient display reference.

## Source requirements

Build from a Git checkout with Git available and complete history. A shallow
checkout gives an incomplete count and is rejected. Fetch its full history
before building, for example with `git fetch --unshallow origin`, or start from
a full clone. A source ZIP without Git history cannot supply verified version
metadata and is not a supported build input. There is no manual version override.

`Directory.Build.props` defines the shared base version and public author
`Zero`. `Directory.Build.targets` derives both plugins' assembly/file versions,
display versions and embedded source attributes from Git, then emits each
assembly's `.build.json` metadata. Do not duplicate those version values in the
individual project files. The UI reads its display version from the built
assembly rather than calculating it from a later checkout.

The values can be inspected from the source checkout with:

```powershell
git rev-parse --is-shallow-repository
git rev-list --count HEAD
git rev-parse --short=7 HEAD
git rev-parse HEAD
```

Commit the reviewed source before making a release artifact. A source hash
cannot identify uncommitted changes. Both the count and hash must describe the
same source commit; do not combine a current count with an older binary or tag.

## Build and publication order

1. Commit the reviewed source and record its full hash and reachable commit
   count. Build, validate and package from that source commit.
2. Review the generated package and repository feed. The feed uses the numeric
   assembly version and links to the padded release tag.
3. Commit the generated feed as a separate metadata update if needed. This
   changes the checkout's HEAD, but does not change the already built artifact.
4. Tag the recorded source commit explicitly and publish the reviewed artifact
   under that tag. Never let the later feed commit become the tag target by
   default, and never overwrite an existing release or its assets.

For example, a source commit whose count is 3 produces an artifact labelled
`0.1.0.0003` with that source hash. A subsequent feed-only commit may have count
4. The release tag still points to source commit 3, and the package still
contains its version and hash. If the next source change has count 5, its
artifact uses `0.1.0.0005`; skipped artifact numbers are expected. Rebuilding
from the feed commit would instead describe count 4 and must not replace the
count-3 artifact.

The core archive name is `OmniKit-<paddedVersion>-<shortSHA>.zip`; its internal
plugin filename remains `OmniKit.dll`. The separate toolbar extension is
`OmniKit.Umbra-<paddedVersion>-<shortSHA>.dll`. Both artifacts must come from the
same source commit. Their release tag is exactly `v<paddedVersion>` and does not
include the short hash.

## Integration checks

Run `pwsh -NoProfile -File tests/VersioningTests.ps1` to exercise the actual
shared MSBuild targets in isolated synthetic repositories. It verifies the
emitted JSON, DLL version resources and embedded commit attributes after one
commit and after a second, metadata-only commit without cleaning the build.
It also verifies rejection of a source directory without Git metadata and a
shallow `file://` clone. The fixtures use synthetic commit identities, change no
global Git settings, and remain under ignored `.local` for inspection.

## Limits and historical releases

Different branches can have equal reachable commit counts, and rewriting
history can change counts. The source hash distinguishes those histories. Stop
if a proposed release would reuse an existing label or move backward in the
numeric version; do not relabel or overwrite the earlier artifact.

.NET assembly metadata limits each numeric component to 65534. If the commit
count exceeds that limit, the build must stop for an explicit version-policy
decision; never wrap or truncate it. This limit comes from
[Microsoft's assembly version documentation](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.assemblyversionattribute?view=net-10.0#remarks).

The earlier `v0.1.0-dev.1` and `v0.1.0-dev.2` releases keep their original tags,
versions and artifacts. This scheme applies to new artifacts and does not
retroactively rename those releases or authorize changing the `0.1.0` base.
