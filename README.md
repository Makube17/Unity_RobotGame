# Robot Grasping Unity Game

## 概要 / Overview

ランダムに積み重なった物体に対して、スキャンによって把持候補を表示し、プレイヤーが適切な候補を選択してロボットハンドで把持を行うUnityゲームです。

This is a Unity game demo in which objects are randomly stacked, grasp candidates are displayed after scanning, and the player selects a suitable grasp position for a robot hand.

## 制作目的 / Purpose

自身の研究テーマである、RGB-D画像を用いた未知物体の認識・把持システムを、ゲーム形式で分かりやすく可視化することを目的としています。

## 使用技術 / Technologies

- Unity
- C#
- Game state management
- Robot hand simulation
- Grasp candidate visualization
- RGB-D based recognition concept

## 主な機能 / Features

- ランダムな物体生成
- 物体生成中・スキャン中の操作ロック
- スキャン操作
- 把持候補の表示
- 候補選択
- ロボットハンドによる把持動作
- 成功・失敗判定

## 工夫した点 / Highlights

- `GameState` によってゲーム進行を管理し、物体生成中やスキャン中に操作できないようにした
- 把持候補を視覚的に表示し、プレイヤーが直感的に選択できるようにした
- 研究内容をそのまま説明するのではなく、体験できるゲーム形式に落とし込んだ

## 現在の進捗 / Current Status

- 物体のランダム生成
- ボタン操作ロック
- スキャン処理の基本フロー
- 把持候補表示の一部

現在も開発中です。

## 今後の改善 / Future Work

- スキャン中の演出追加
- AI側との連携改善
- 把持成功・失敗判定の改善
- UI表示の調整
- 候補位置・角度の補正

## 動作動画 / Demo Video

後日追加予定

## 備考 / Notes

このリポジトリは、個人制作のUnity作品を企業提出用に整理したものです。
