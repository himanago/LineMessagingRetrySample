using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace LineMessagingRetrySample
{
    public class CosmosTriggerMessager
    {
        private readonly HttpClient _httpClient;
        private readonly string _channelId;
        private readonly string _channelSecret;

        public CosmosTriggerMessager(IHttpClientFactory httpClientFactory, LineBotSettings settings)
        {
            _httpClient = httpClientFactory.CreateClient("line");
            _channelId = settings.ChannelId;
            _channelSecret = settings.ChannelSecret;
        }

        //[FixedDelayRetry(-1, "00:00:15")]
        [ExponentialBackoffRetry(-1, "00:00:04", "00:15:00")]
        [FunctionName(nameof(CosmosTriggerMessager))]
        public async Task Run([CosmosDBTrigger(
            databaseName: "messagedb",
            collectionName: "messages",
            ConnectionStringSetting = "cosmosDbConnectionString",
            CreateLeaseCollectionIfNotExists = true,
            LeaseCollectionName = "leases")]IReadOnlyList<Document> input, ILogger log)
        {
            var documents = input.Select(x => (MessageDocument)(dynamic)x);

            // メッセージ（最大5件ずつ）
            foreach (var messages in documents.Select((doc, idx) => (doc, idx))
                .GroupBy(x => x.idx / 5)
                .Select(x => x.Select(x => new { type = "text", text = x.doc.Text })))
            {
                var json = JsonSerializer.Serialize(
                    new { messages = messages },
                    new JsonSerializerOptions
                    {
                        IgnoreNullValues = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                        WriteIndented = true
                    });

                // ログ
                log.LogInformation(json);

                // チャネルアクセストークンを取得
                var channelAccessToken = await GetChannelAccessTokenAsync();

                // 50%の確率で失敗する！-> エラーで送信できないケースを想定
                if (new Random().Next(0, 2) == 0)
                {
                    throw new Exception("エラーが出て送信失敗しました。");
                }

                // メッセージ送信
                var request = new HttpRequestMessage(
                    HttpMethod.Post, "/v2/bot/message/broadcast");
                request.Headers.Add("Authorization", "Bearer " + channelAccessToken);
                request.Headers.Add("X-Line-Retry-Key", input[0].Id);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.SendAsync(request);

                log.LogInformation($"Response status: {response.StatusCode}");

                // 500番台だった場合は例外を投げる
                if ((int)response.StatusCode >= 500)
                {
                    response.EnsureSuccessStatusCode();
                }
                // 400番台だった場合は終了
                else if ((int)response.StatusCode >= 400)
                {
                    return;
                }

                // 50%の確率で失敗する！-> エラーが出たが送信できていたケースを想定
                if (new Random().Next(0, 2) == 0)
                {
                    throw new Exception("エラーが出たけど送信成功しました。");
                }

                // 「エラーが出て送信失敗」50%
                // 「エラーが出るけど送信成功」25%
                // 「エラーなしで送信成功」25%
            }
        }

        private async Task<string> GetChannelAccessTokenAsync()
        {
            var response = await _httpClient.PostAsync("/v2/oauth/accessToken", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _channelId,
                ["client_secret"] = _channelSecret,
            }));
            var json = await response.Content.ReadAsStringAsync();
            var payload = JsonSerializer.Deserialize<ChannelAccessTokenPayload>(json);
            return payload.AccessToken;
        }
    }

    public class MessageDocument
    {
        public string Id { get; set; }
        public string Text { get; set; }
    }

    public class ChannelAccessTokenPayload
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }
        [JsonPropertyName("expires_in")]
        public long ExpiresIn { get; set; }
        [JsonPropertyName("token_type")]
        public string TokenType { get; set; }
    }
}
