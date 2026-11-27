using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PipelineGenWpf.Models;

namespace PipelineGenWpf.Services
{
    public class OpenRouterService
    {
        private static readonly HttpClient Client;

        static OpenRouterService()
        {
            App.ConfigureNetworkDefaults();

            HttpClientHandler handler = new HttpClientHandler();
            handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            handler.UseProxy = true;
            handler.UseDefaultCredentials = true;

            Client = new HttpClient(handler);
            Client.Timeout = Timeout.InfiniteTimeSpan;
        }

        private static string UnwrapExceptionMessage(Exception ex)
        {
            if (ex == null) return "Неизвестная ошибка сети";

            List<string> parts = new List<string>();
            Exception cur = ex;
            int guard = 0;
            while (cur != null && guard < 6)
            {
                string msg = cur.Message;
                if (!string.IsNullOrWhiteSpace(msg) &&
                    !string.Equals(msg, "An error occurred while sending the request.", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(msg, "Произошла ошибка при отправке запроса.", StringComparison.OrdinalIgnoreCase))
                {
                    if (!parts.Contains(msg))
                    {
                        parts.Add(msg);
                    }
                }
                cur = cur.InnerException;
                guard++;
            }

            if (parts.Count == 0)
            {
                return ex.Message;
            }
            return string.Join(" -> ", parts.ToArray());
        }

        private static async Task<HttpResponseMessage> SendWithTimeoutAsync(
            Func<HttpRequestMessage> requestFactory,
            TimeSpan timeout,
            int maxRetries)
        {
            Exception lastEx = null;
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                int retryDelayMs = 0;
                using (CancellationTokenSource cts = new CancellationTokenSource(timeout))
                using (HttpRequestMessage req = requestFactory())
                {
                    try
                    {
                        HttpResponseMessage resp = await Client.SendAsync(req, HttpCompletionOption.ResponseContentRead, cts.Token);
                        int code = (int)resp.StatusCode;
                        if (attempt < maxRetries && (code == 429 || code == 502 || code == 503 || code == 504))
                        {
                            resp.Dispose();
                            retryDelayMs = 700 * (attempt + 1);
                        }
                        else
                        {
                            return resp;
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        lastEx = new TimeoutException(string.Format("Превышен таймаут ожидания ответа ({0} сек.).", (int)timeout.TotalSeconds));
                        if (attempt < maxRetries)
                        {
                            retryDelayMs = 500;
                        }
                        else
                        {
                            throw lastEx;
                        }
                    }
                    catch (HttpRequestException hex)
                    {
                        lastEx = new Exception("Ошибка сети: " + UnwrapExceptionMessage(hex), hex);
                        if (attempt < maxRetries)
                        {
                            retryDelayMs = 500;
                        }
                        else
                        {
                            throw lastEx;
                        }
                    }
                }

                if (retryDelayMs > 0)
                {
                    await Task.Delay(retryDelayMs);
                }
            }

            throw lastEx ?? new Exception("Не удалось выполнить HTTP-запрос.");
        }

        private static void ApplyOpenRouterHeaders(HttpRequestMessage req, string apiKey)
        {
            string cleanKey = (apiKey ?? "").Trim();
            if (!string.IsNullOrEmpty(cleanKey))
            {
                req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + cleanKey);
            }
            req.Headers.TryAddWithoutValidation("HTTP-Referer", "https://pipelinegen.local");
            req.Headers.TryAddWithoutValidation("X-Title", "PipelineGen");
            req.Headers.TryAddWithoutValidation("Accept", "application/json");
        }

        public async Task<string> TestModelAsync(string apiKey, string modelId)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("Не указан API-ключ OpenRouter.");
            }
            if (string.IsNullOrWhiteSpace(modelId))
            {
                throw new InvalidOperationException("Не указан ID модели.");
            }

            Dictionary<string, object> payload = new Dictionary<string, object>();
            payload["model"] = modelId.Trim();
            payload["messages"] = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    { "role", "user" },
                    { "content", "hi" }
                }
            };
            payload["max_tokens"] = 16;

            string json = JsonHelper.SerializeRaw(payload);

            using (HttpResponseMessage resp = await SendWithTimeoutAsync(
                delegate
                {
                    HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions");
                    ApplyOpenRouterHeaders(req, apiKey);
                    req.Content = new StringContent(json, Encoding.UTF8, "application/json");
                    return req;
                },
                TimeSpan.FromSeconds(30),
                1))
            {
                string body = (await resp.Content.ReadAsStringAsync() ?? "").Trim();
                string embeddedError = ExtractOpenRouterError(body);

                if (!resp.IsSuccessStatusCode)
                {
                    if (!string.IsNullOrEmpty(embeddedError))
                    {
                        throw new Exception(string.Format("API ({0}): {1}", (int)resp.StatusCode, embeddedError));
                    }
                    throw new Exception(string.Format("API HTTP {0}: {1}", (int)resp.StatusCode, Truncate(body, 260)));
                }

                if (!string.IsNullOrEmpty(embeddedError))
                {
                    throw new Exception("API: " + embeddedError);
                }

                return "ok";
            }
        }

        public async Task<List<CatalogModelItem>> FetchCatalogAsync(string apiKey)
        {
            using (HttpResponseMessage resp = await SendWithTimeoutAsync(
                delegate
                {
                    HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, "https://openrouter.ai/api/v1/models");
                    ApplyOpenRouterHeaders(req, apiKey);
                    return req;
                },
                TimeSpan.FromSeconds(25),
                1))
            {
                string body = (await resp.Content.ReadAsStringAsync() ?? "").Trim();
                if (!resp.IsSuccessStatusCode)
                {
                    string err = ExtractOpenRouterError(body);
                    throw new Exception(string.Format("HTTP {0}: {1}", (int)resp.StatusCode, string.IsNullOrEmpty(err) ? Truncate(body, 200) : err));
                }

                List<CatalogModelItem> result = new List<CatalogModelItem>();
                IDictionary<string, object> root = JsonHelper.DeserializeObject(body) as IDictionary<string, object>;
                if (root != null && root.ContainsKey("data") && root["data"] is IEnumerable)
                {
                    foreach (object entry in (IEnumerable)root["data"])
                    {
                        IDictionary<string, object> m = entry as IDictionary<string, object>;
                        if (m == null) continue;
                        string id = JsonHelper.GetString(m, "id", "");
                        if (string.IsNullOrEmpty(id) || id.IndexOf(":batch", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            continue;
                        }

                        string name = JsonHelper.GetString(m, "name", id);
                        bool isVision = false;

                        object archObj;
                        if (m.TryGetValue("architecture", out archObj))
                        {
                            IDictionary<string, object> arch = archObj as IDictionary<string, object>;
                            if (arch != null)
                            {
                                string modality = JsonHelper.GetString(arch, "modality", "");
                                if (modality.IndexOf("image", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    isVision = true;
                                }
                            }
                        }

                        if (!isVision)
                        {
                            string lower = id.ToLowerInvariant();
                            if (lower.Contains("vision") || lower.Contains("-vl-") || lower.Contains("vl-"))
                            {
                                isVision = true;
                            }
                        }

                        result.Add(new CatalogModelItem
                        {
                            Id = id,
                            Name = name,
                            IsVision = isVision
                        });
                    }
                }

                return result;
            }
        }

        public async Task<string> ExecuteChatAsync(string apiKey, string model, List<ChatMessageItem> messages)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("API-ключ OpenRouter не указан.");
            }
            if (string.IsNullOrWhiteSpace(model))
            {
                throw new InvalidOperationException("Не указана модель для шага.");
            }

            List<Dictionary<string, object>> msgPayloads = new List<Dictionary<string, object>>();
            foreach (ChatMessageItem item in messages)
            {
                Dictionary<string, object> dict = new Dictionary<string, object>();
                dict["role"] = item.Role;
                dict["content"] = item.Content;
                msgPayloads.Add(dict);
            }

            Dictionary<string, object> payload = new Dictionary<string, object>();
            payload["model"] = model.Trim();
            payload["messages"] = msgPayloads;

            string json = JsonHelper.SerializeRaw(payload);

            using (HttpResponseMessage resp = await SendWithTimeoutAsync(
                delegate
                {
                    HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions");
                    ApplyOpenRouterHeaders(req, apiKey);
                    req.Content = new StringContent(json, Encoding.UTF8, "application/json");
                    return req;
                },
                TimeSpan.FromSeconds(300),
                1))
            {
                string body = (await resp.Content.ReadAsStringAsync() ?? "").Trim();
                string embeddedError = ExtractOpenRouterError(body);

                if (!resp.IsSuccessStatusCode)
                {
                    if (!string.IsNullOrEmpty(embeddedError))
                    {
                        throw new Exception(FormatApiError((int)resp.StatusCode, model, embeddedError));
                    }
                    throw new Exception(string.Format("OpenRouter HTTP {0}: {1}", (int)resp.StatusCode, Truncate(body, 280)));
                }

                if (!string.IsNullOrEmpty(embeddedError))
                {
                    throw new Exception(FormatApiError(0, model, embeddedError));
                }

                IDictionary<string, object> root = JsonHelper.DeserializeObject(body) as IDictionary<string, object>;
                if (root == null || !root.ContainsKey("choices"))
                {
                    throw new Exception("Некорректный ответ API: " + Truncate(body, 220));
                }

                IEnumerable choices = root["choices"] as IEnumerable;
                if (choices != null)
                {
                    foreach (object c in choices)
                    {
                        IDictionary<string, object> choiceDict = c as IDictionary<string, object>;
                        if (choiceDict == null || !choiceDict.ContainsKey("message")) continue;
                        IDictionary<string, object> msgDict = choiceDict["message"] as IDictionary<string, object>;
                        if (msgDict == null) continue;

                        string extracted = ExtractMessageText(msgDict);
                        if (!string.IsNullOrEmpty(extracted))
                        {
                            return extracted;
                        }

                        string refusal = JsonHelper.GetString(msgDict, "refusal", "");
                        if (!string.IsNullOrEmpty(refusal))
                        {
                            throw new Exception("Отказ модели: " + refusal);
                        }

                        return "";
                    }
                }

                throw new Exception("Модель вернула пустой ответ (choices пуст).");
            }
        }

        private static string ExtractMessageText(IDictionary<string, object> msgDict)
        {
            if (msgDict.ContainsKey("content") && msgDict["content"] != null)
            {
                object contentObj = msgDict["content"];
                string asString = contentObj as string;
                if (asString != null)
                {
                    if (!string.IsNullOrEmpty(asString))
                    {
                        return asString;
                    }
                }
                else if (contentObj is IEnumerable)
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (object part in (IEnumerable)contentObj)
                    {
                        IDictionary<string, object> pDict = part as IDictionary<string, object>;
                        if (pDict != null)
                        {
                            string t = JsonHelper.GetString(pDict, "text", "");
                            if (!string.IsNullOrEmpty(t))
                            {
                                if (sb.Length > 0) sb.Append("\n");
                                sb.Append(t);
                            }
                        }
                    }
                    if (sb.Length > 0) return sb.ToString();
                }
            }

            // Fallback if reasoning model returned only reasoning text
            string reasoning = JsonHelper.GetString(msgDict, "reasoning", "");
            if (!string.IsNullOrEmpty(reasoning))
            {
                return reasoning;
            }

            return "";
        }

        private static string ExtractOpenRouterError(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return "";
            try
            {
                IDictionary<string, object> root = JsonHelper.DeserializeObject(body) as IDictionary<string, object>;
                if (root != null && root.ContainsKey("error") && root["error"] != null)
                {
                    IDictionary<string, object> errObj = root["error"] as IDictionary<string, object>;
                    if (errObj != null)
                    {
                        string msg = JsonHelper.GetString(errObj, "message", "");
                        string rawDetail = "";
                        object metaObj;
                        if (errObj.TryGetValue("metadata", out metaObj))
                        {
                            IDictionary<string, object> meta = metaObj as IDictionary<string, object>;
                            if (meta != null)
                            {
                                rawDetail = JsonHelper.GetString(meta, "raw", "");
                            }
                        }
                        if (!string.IsNullOrEmpty(rawDetail) && !string.Equals(rawDetail, msg, StringComparison.OrdinalIgnoreCase))
                        {
                            return string.IsNullOrEmpty(msg) ? Truncate(rawDetail, 240) : (msg + " — " + Truncate(rawDetail, 180));
                        }
                        return msg;
                    }
                    return Convert.ToString(root["error"]);
                }
            }
            catch
            {
            }
            return "";
        }

        private static string FormatApiError(int statusCode, string model, string embeddedError)
        {
            string lower = (embeddedError ?? "").ToLowerInvariant();
            if (lower.Contains("image input") || lower.Contains("support image"))
            {
                return string.Format(
                    "Модель «{0}» не поддерживает изображения (Vision). Откройте Конструктор и выберите модель с [Vision] (например: qwen/qwen3-vl-8b-instruct, openai/gpt-4o-mini, anthropic/claude-sonnet-5.5).",
                    model);
            }
            if (statusCode > 0)
            {
                return string.Format("OpenRouter ({0}): {1}", statusCode, embeddedError);
            }
            return "OpenRouter: " + embeddedError;
        }

        private static string Truncate(string s, int maxLen)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\r", " ").Replace("\n", " ").Trim();
            if (s.Length <= maxLen) return s;
            return s.Substring(0, maxLen) + "...";
        }
    }
}
