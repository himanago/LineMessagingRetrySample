# メッセージ配信サンプル（C#）
## 概要
Azure Cosmos DB・Functions を用いて LINE Messaging API に安全にメッセージ配信する機能のサンプルコードです。

## 使い方
- LINE Messaging API チャネルを作成し、自身の LINE に友だち追加しておく
- Azure に Cosmos DB アカウントを用意し、データベース名 `messagedb` 、コンテナー名 `messages` で DB・コンテナーを作成します。
- 本コードを Azure Functions にデプロイ（.NET Core 3.1）
- Azure Functions のアプリケーション設定に以下を追加
  - `LineBotSettings:ChannelId`: Messaging API のチャネル ID
  - `LineBotSettings:ChannelSecret`: Messaging API のチャネルシークレット
  - `cosmosDbConnectionString`: Cosmos DB の接続文字列
- Cosmos DB のコンテナーに以下の形式でデータを追加すると、一定確率でエラーが発生しますが再試行によりほぼ確実に LINE にメッセージが配信されます。

```json
{
  "text": "送信したいメッセージ"
}
```

