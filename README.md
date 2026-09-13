# PortalNES

PortalNES is an experimental NES emulator that converts NES backgrounds and
sprites into a configurable 3D scene in Unity.

The standalone 2D and 3D scenes can be built and run without the Portalgraph SDK.

The commercial **Portalgraph SDK is not included in this repository**. It is
only required to use Portalgraph's multi-view 3D display features. Get it here:

https://portalgraph.booth.pm/items/6256749

https://portalgraph.itch.io/portalgraph-personal

ROM images and game-specific profile files are not included. Use only ROM data
that you are legally entitled to use. See `PortalNES_Manual.txt` for controls,
supported mappers, and profile editing instructions.

## Quick start in Unity

Open or create a Unity scene, then choose one of the following from the Unity
menu:

- `PortalNes > Create 2D Demo Rig` creates a configured NES runner, input
  provider, texture renderer, and screen quad.
- `PortalNes > Create 3D Demo Rig` creates a configured NES runner, input
  provider, and 3D scene renderer.

Enter Play Mode and press F1 to select a local ROM. Alternatively, set
`Rom Path` in the `NesRunner` Inspector and enable `Load Rom On Start`.

## License

The source code and original assets contained in this repository are available
under the MIT License. See `LICENSE` for details.

This license does not apply to Portalgraph, game ROMs, game graphics, audio,
profiles derived from copyrighted games, or other third-party materials. Such
materials remain subject to their respective licenses and rights holders.

## 日本語

PortalNESは、ファミコンの背景とスプライトを設定可能な3Dシーンへ変換する、
Unity製の実験的なエミュレーターです。

通常の2D・3Dシーンは、Portalgraph SDKなしでビルド・実行できます。

有償配布物である **Portalgraph SDKは、このリポジトリに含まれていません**。
Portalgraphによる多視点3D表示を利用する場合のみ必要です。以下から入手できます。

https://portalgraph.booth.pm/items/6256749

https://portalgraph.itch.io/portalgraph-personal

ROMイメージおよびゲーム固有のプロファイルは同梱していません。
利用する権利のあるROMデータだけを使用してください。操作方法、対応マッパー、
プロファイル編集については `PortalNES_Manual.txt` を参照してください。

## Unityでの開始方法

Unityでシーンを開くか新規作成し、メニューから次のいずれかを実行します。

- `PortalNes > Create 2D Demo Rig`：設定済みのNESランナー、入力、
  テクスチャ描画および画面用Quadを作成します。
- `PortalNes > Create 3D Demo Rig`：設定済みのNESランナー、入力および
  3Dシーンレンダラーを作成します。

Play Modeに入り、F1でローカルのROMを選択してください。または、`NesRunner`の
Inspectorで`Rom Path`を指定し、`Load Rom On Start`を有効にすると起動時に
読み込めます。

## ライセンス

このリポジトリに含まれるソースコードおよび独自制作アセットには、MIT Licenseが
適用されます。詳細は `LICENSE` を参照してください。

このライセンスは、Portalgraph、ゲームROM、ゲームの画像・音声、著作物から
派生したプロファイル、その他の第三者制作物には適用されません。それらには
各権利者および各ライセンスの条件が適用されます。
