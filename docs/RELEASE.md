# リリース手順

Muu の新バージョンを **GitHub Releases** と **winget** に公開する手順。

---

## 1. 事前準備（初回のみ）

### 必要なツール

| ツール | 用途 | インストール |
|-------|------|-------------|
| **.NET 10 SDK** | ビルド | `winget install Microsoft.DotNet.SDK.10` |
| **GitHub CLI (`gh`)** | Release 作成 | `winget install GitHub.cli` |
| **wingetcreate** | winget マニフェスト送信 | `winget install Microsoft.WingetCreate` |
| **ImageMagick** | アイコン .ico 生成（任意） | `winget install ImageMagick.ImageMagick` |

### 認証セットアップ

```powershell
gh auth login         # GitHub にログイン
gh auth status        # `repo` スコープがあることを確認
```

### winget の Publisher

- **Publisher**: `foolsgold`
- **PackageIdentifier**: `foolsgold.Muu`
- **マニフェスト保存先**: `winget/manifests/f/foolsgold/Muu/<version>/`

---

## 2. リリース前チェックリスト

新バージョンを `X.Y.Z` とする。

- [ ] 機能追加・バグ修正が `main` にマージ済み
- [ ] ローカルで動作確認済み (`dotnet run --project src/Muu`)
- [ ] `src/Muu/Muu.csproj` のバージョン番号を更新
  - `<Version>X.Y.Z</Version>`
  - `<AssemblyVersion>X.Y.Z.0</AssemblyVersion>`
  - `<FileVersion>X.Y.Z.0</FileVersion>`
- [ ] `README.md` の更新（必要なら）
- [ ] バージョン更新を commit & push

```powershell
git add src/Muu/Muu.csproj README.md
git commit -m "chore: bump version to X.Y.Z"
git push
```

---

## 3. リリースビルド作成

### Self-contained single-file ビルド

エンドユーザーが .NET ランタイムをインストール不要にする。

```powershell
dotnet publish src/Muu/Muu.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:SelfContained=true `
  -p:EnableCompressionInSingleFile=true `
  -p:DebugType=embedded `
  -o publish
```

成果物: `publish/Muu.exe`（圧縮済み単一実行ファイル、約80MB）

### 検証

- 別マシン（または .NET 未インストール環境）で動作確認推奨
- 最低限、自分の PC で起動 → `Win+Ctrl+Alt+M` でランチャー表示できることを確認

---

## 4. GitHub Release 作成

### タグを切ってプッシュ

```powershell
git tag -a vX.Y.Z -m "Muu vX.Y.Z"
git push origin vX.Y.Z
```

### Release 作成

```powershell
gh release create vX.Y.Z publish/Muu.exe `
  --title "Muu vX.Y.Z" `
  --notes "リリースノート本文..."
```

リリースノートの参考フォーマット:

```markdown
## 主な変更

- 〇〇機能を追加
- ✕✕のバグを修正

## インストール

`Muu.exe` をダウンロードして任意のフォルダに配置するだけ。.NET ランタイム不要。

## 動作環境

- Windows 10 / 11 (x64)
```

### URL とハッシュを取得

```powershell
# ダウンロード URL
$url = "https://github.com/yanqirenshi/muu/releases/download/vX.Y.Z/Muu.exe"

# SHA256 計算（次の winget マニフェスト用）
sha256sum publish/Muu.exe
```

---

## 5. winget マニフェスト更新と提出

### A. wingetcreate で自動更新（**推奨**）

新バージョンの URL から既存マニフェストを更新し、`microsoft/winget-pkgs` に PR を自動送信する。

```powershell
$token = gh auth token

wingetcreate update foolsgold.Muu `
  --version X.Y.Z `
  --urls https://github.com/yanqirenshi/muu/releases/download/vX.Y.Z/Muu.exe `
  --submit `
  --token $token
```

これで終わり。
- 自動的にハッシュ計算 → マニフェスト更新 → fork → PR 作成

### B. 手動でマニフェスト更新する場合

`winget/manifests/f/foolsgold/Muu/X.Y.Z/` に 3 ファイル作成:

```
foolsgold.Muu.yaml              # version
foolsgold.Muu.installer.yaml    # installer (URL, SHA256)
foolsgold.Muu.locale.en-US.yaml # 説明、ライセンス等
```

既存バージョン (例: `0.1.0/`) のファイルをコピーして以下を変更:

- `PackageVersion: X.Y.Z`
- `ReleaseDate: YYYY-MM-DD`
- `InstallerUrl`: 新 Release のダウンロード URL
- `InstallerSha256`: `sha256sum publish/Muu.exe` の結果（**大文字**）
- `ReleaseNotesUrl`: 新タグの URL

検証 → 提出:

```powershell
winget validate --manifest winget/manifests/f/foolsgold/Muu/X.Y.Z

$token = gh auth token
wingetcreate submit `
  --prtitle "New version: foolsgold.Muu version X.Y.Z" `
  --token $token `
  --no-open `
  winget/manifests/f/foolsgold/Muu/X.Y.Z
```

---

## 6. PR 後の対応

### CLA（初回のみ）

新規貢献者の場合、Microsoft の CLA 同意を求められる。PR にコメント:

```
@microsoft-github-policy-service agree
```

2 回目以降のリリースは不要。

### 自動チェック

`microsoft/winget-pkgs` 側で以下が走る:

1. **License/CLA** — 署名済みなら ✅
2. **Manifest schema** — `winget validate` で事前確認していれば ✅
3. **Installer URL & SHA256** — ダウンロードと整合性チェック
4. **SmartScreen / ウイルススキャン** — 安全性検証
5. **モデレーターレビュー** — 通常 1〜数日（更新は早い）

問題があれば PR にコメントが付くので対応する。

### マージ後の確認

```powershell
winget source update
winget search foolsgold.Muu
winget upgrade foolsgold.Muu     # 既存ユーザーは更新可能に
```

---

## 7. winget マニフェストを repo に同期

PR がマージされたら、`microsoft/winget-pkgs` に取り込まれた最終的なマニフェストを
`winget/manifests/f/foolsgold/Muu/X.Y.Z/` に反映してコミット（記録目的）:

```powershell
git add winget/
git commit -m "chore: sync winget manifest for vX.Y.Z"
git push
```

---

## トラブルシューティング

| 症状 | 対処 |
|------|------|
| `winget validate` がエラー | YAML インデントとフィールド名を確認。スキーマ URL の `1.6.0` を使用 |
| `wingetcreate submit` で 401 | `gh auth refresh -s repo` で `repo` スコープを追加 |
| PR で `Validation-Installer-Error` | InstallerUrl にアクセスできるか、SHA256 が一致するか確認 |
| PR で `Manifest-Validation-Error` | フィールド型・必須項目を再確認 |
| マージ後 `winget search` で見つからない | `winget source update` を実行。インデックス反映に数時間かかる場合あり |

---

## リリースの参考履歴

| バージョン | 日付 | リリース URL | winget PR |
|-----------|------|-------------|-----------|
| 0.1.0 | 2026-04-25 | [v0.1.0](https://github.com/yanqirenshi/muu/releases/tag/v0.1.0) | [#365146](https://github.com/microsoft/winget-pkgs/pull/365146) |

---

## 参考リンク

- [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs) — winget 公式リポジトリ
- [winget マニフェストスキーマ](https://github.com/microsoft/winget-cli/tree/master/doc/ManifestSpecv1.6.md)
- [wingetcreate ドキュメント](https://github.com/microsoft/winget-create)
- [.NET 単一ファイル発行](https://learn.microsoft.com/dotnet/core/deploying/single-file/overview)
