# Robot Grasping Unity Game

## 概要 / Overview

ランダムに積み重なった物体に対して、RGB-D画像を用いた把持候補推定を行い、プレイヤーが候補を選択してロボットハンドで把持を行うUnityゲームデモです。

This is a Unity game demo that visualizes an RGB-D based robotic grasping system. The player scans randomly stacked objects, selects a grasp candidate, and executes a grasp motion with a simulated robot hand.

## Demo Video

<video src="docs/demo.mp4" controls width="720"></video>

動画が表示されない場合はこちらから再生できます。  
[Demo Video](docs/demo.mp4)

## 制作目的 / Purpose

自身の研究テーマである、RGB-D画像を用いた未知物体の認識・把持システムを、ゲーム形式で分かりやすく体験できる形に落とし込むことを目的としています。

単に研究内容を説明するのではなく、スキャン、把持候補の表示、候補選択、ロボットハンドによる把持動作までを一連のインタラクションとして実装しています。

## 使用技術 / Technologies

- Unity
- C#
- Robot hand simulation
- RGB-D image based grasping concept
- Dex-Net based grasp candidate estimation
- HTTP / JSON communication with external AI server
- Game state management
- Grasp candidate visualization

## システム構成 / System Overview

Unity側では、物体生成、スキャン操作、把持候補の表示、候補選択、ロボットハンドの動作、成功・失敗判定を担当します。

AIサーバー側では、RGB-D画像を受け取り、Dex-Netを用いて把持候補を推定し、把持中心、深度、角度、評価値をUnityへ返します。

処理の流れは以下の通りです。

- Unity上で物体をランダム生成
- RGB-D画像を取得
- AIサーバーへ画像を送信
- Dex-Netにより把持候補を推定
- Unity上に把持候補マーカーを表示
- プレイヤーが候補を選択
- 把持位置をロボットハンド座標系へ変換
- ロボットハンドが把持動作を実行
- 把持成功・失敗を判定

## 主な機能 / Features

- ランダムな物体生成
- 物体生成中・スキャン中の操作ロック
- スキャン処理
- RGB-D画像の送信
- Dex-Netによる把持候補の取得
- 把持候補マーカーの表示
- プレイヤーによる候補選択
- Unity座標系からロボットハンド座標系への変換
- ロボットハンドによる把持動作
- 簡易的な成功・失敗判定
- デバッグログによる座標変換の確認

## 主なスクリプト / Main Scripts

- `ScanManager.cs`  
  スキャン処理、AIサーバーとの通信、把持候補の表示、候補選択、成功判定などを管理します。

- `RobotHandController.cs`  
  ロボットハンドの回転、把持動作、固定ベース時の座標変換・角度計算を管理します。

- `ImageSender.cs`  
  RGB-D画像や関連データをAIサーバーへ送信する処理を担当します。

- `ObjectSpawner.cs`  
  物体のランダム生成を担当します。

- `SpawnedObjectPhysicsStabilizer.cs`  
  生成された物体の物理挙動を調整し、デモ中に物体が過度に弾かれにくくするための補助スクリプトです。

## 工夫した点 / Highlights

- `GameState` によってゲーム進行を管理し、物体生成中やスキャン中に不適切な操作ができないようにしました。
- 把持候補をマーカーとして表示し、プレイヤーが直感的に候補を選択できるようにしました。
- Dex-Netの出力である画像座標、深度値、把持角度をUnity上の把持動作へつなげる処理を実装しました。
- Unity座標系とロボットハンド座標系の対応関係を整理し、把持位置をハンド基準の座標へ変換できるようにしました。
- 研究システムをそのまま再現するのではなく、提出用デモとして遊べる形に簡略化しました。
- デバッグログを追加し、Dex-Netの出力、Unity上の把持位置、ハンド座標系での把持位置、角度計算を確認できるようにしました。

## 現在の進捗 / Current Status

- 物体のランダム生成
- スキャン操作
- AIサーバーとの連携
- Dex-Netによる把持候補の取得
- 把持候補の可視化
- 候補選択
- ロボットハンドの把持動作
- 簡易的な成功・失敗判定
- デモ用の動作調整

## 制限事項 / Limitations

現在の実装は提出用デモとして簡略化しており、実機ロボットと完全に同じ制御や物理挙動を再現するものではありません。

特に、Unity上のロボットハンドモデルはBlenderで作成したモデルを使用しているため、見た目上の指先位置と内部座標の整合性にずれが生じる場合があります。そのため、把持位置の計算にはデモ用の固定値やオフセット調整を用いています。

また、成功判定も厳密な物理シミュレーションではなく、ゲームデモとして成立するように簡易化しています。

## 今後の改善 / Future Work

- ロボットハンドモデルの座標系・指先位置の修正
- 把持成功・失敗判定の精度向上
- Dex-Net候補とUnity内マーカー表示の整合性向上
- 把持角度をより自然に反映したマーカー表示
- 物体の物理挙動の調整
- UIや演出の改善
- 実機ロボット制御との対応関係の整理

## 備考 / Notes

このリポジトリは、個人制作のUnity作品を企業提出用に整理したものです。

研究で扱っているRGB-D画像を用いた未知物体把持の流れを、Unity上で視覚的・体験的に示すことを目的としています。
