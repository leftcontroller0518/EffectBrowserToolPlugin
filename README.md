# エフェクト一覧プラグイン
YMM4（ゆっくりMovieMaker4）の「ツール」メニューから呼び出せる、
映像エフェクト／音声エフェクトの一覧を表示・お気に入り管理・検索・即時適用するプラグインです。

## 主な機能
- **8つのタブ構成**:
  - **映像**: 映像エフェクト一覧
  - **音声**: 音声エフェクト一覧
  - **お気に入り**: お気に入り登録したエフェクト一覧
  - **履歴**: 最近使用したエフェクト履歴（直近20件）
  - **フォルダ**: カスタムフォルダによるエフェクト分類
  - **プリセット**: カスタムパラメータ付きプリセット
  - **適用中**: 選択アイテムの適用エフェクト管理
  - **検索**: エフェクトのリアルタイムキーワード検索
- **お気に入り機能**: 各エフェクトの「★ / ☆」ボタンでワンクリック登録。ローカルJSON (`%APPDATA%\YMM4EffectBrowserPlugin\user_data.json`) に自動保存。
- **選択アイテムへの即時適用**: リスト項目の「適用」ボタンクリック、または項目ダブルクリックでタイムライン上の選択アイテムへ適用。
- **UI仮想化 & 超高速読み込み**: `VirtualizingStackPanel` による省メモリ・高速スクロール対応。

## ファイル構成
```
EffectBrowserToolPlugin/
├── Directory.Build.props
├── README.md
└── EffectBrowserToolPlugin/
    ├── EffectBrowserToolPlugin.cs        # IToolPlugin実装
    ├── EffectBrowserToolViewModel.cs     # ViewModel（コレクション・お気に入り・履歴・適用処理）
    ├── EffectBrowserToolView.xaml        # UIレイアウト（8タブ構成）
    ├── EffectBrowserToolView.xaml.cs     # View コードビハインド
    ├── EffectEntry.cs                    # データモデル
    ├── ActiveEffectViewModel.cs          # 適用中エフェクト管理ViewModel
    ├── EffectLocalizer.cs                # 軽量文字列整形
    ├── InputDialog.xaml                  # 入力ダイアログUI
    ├── InputDialog.xaml.cs               # 入力ダイアログ コードビハインド
    └── UserDataManager.cs                # お気に入り・履歴のJSON永続化マネージャー
```
