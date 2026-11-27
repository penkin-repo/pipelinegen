using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Web.Script.Serialization;
using PipelineGenWpf.Models;

namespace PipelineGenWpf.Services
{
    public static class JsonHelper
    {
        private static JavaScriptSerializer CreateSerializer()
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            serializer.RecursionLimit = 100;
            return serializer;
        }

        public static string SerializeRaw(object obj)
        {
            return CreateSerializer().Serialize(obj);
        }

        public static object DeserializeObject(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }
            return CreateSerializer().DeserializeObject(json);
        }

        public static string PrettyPrint(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return json;
            }

            StringBuilder sb = new StringBuilder(json.Length + 256);
            int indent = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = 0; i < json.Length; i++)
            {
                char ch = json[i];
                if (inString)
                {
                    sb.Append(ch);
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (ch == '\\')
                    {
                        escaped = true;
                    }
                    else if (ch == '"')
                    {
                        inString = false;
                    }
                }
                else
                {
                    switch (ch)
                    {
                        case '"':
                            inString = true;
                            sb.Append(ch);
                            break;
                        case '{':
                        case '[':
                            sb.Append(ch);
                            int nextNonWs = FindNextNonWhitespace(json, i + 1);
                            if (nextNonWs >= 0 && ((ch == '{' && json[nextNonWs] == '}') || (ch == '[' && json[nextNonWs] == ']')))
                            {
                                sb.Append(json[nextNonWs]);
                                i = nextNonWs;
                            }
                            else
                            {
                                indent++;
                                sb.Append("\r\n");
                                AppendIndent(sb, indent);
                            }
                            break;
                        case '}':
                        case ']':
                            indent = Math.Max(0, indent - 1);
                            sb.Append("\r\n");
                            AppendIndent(sb, indent);
                            sb.Append(ch);
                            break;
                        case ',':
                            sb.Append(ch);
                            sb.Append("\r\n");
                            AppendIndent(sb, indent);
                            break;
                        case ':':
                            sb.Append(": ");
                            break;
                        case ' ':
                        case '\t':
                        case '\r':
                        case '\n':
                            break;
                        default:
                            sb.Append(ch);
                            break;
                    }
                }
            }

            return sb.ToString();
        }

        private static int FindNextNonWhitespace(string s, int start)
        {
            for (int i = start; i < s.Length; i++)
            {
                char c = s[i];
                if (c != ' ' && c != '\t' && c != '\r' && c != '\n')
                {
                    return i;
                }
            }
            return -1;
        }

        private static void AppendIndent(StringBuilder sb, int indent)
        {
            for (int i = 0; i < indent; i++)
            {
                sb.Append("  ");
            }
        }

        public static string GetString(IDictionary<string, object> dict, string key, string fallback)
        {
            if (dict == null || !dict.ContainsKey(key) || dict[key] == null)
            {
                return fallback;
            }
            return Convert.ToString(dict[key]);
        }

        public static bool GetBool(IDictionary<string, object> dict, string key, bool fallback)
        {
            if (dict == null || !dict.ContainsKey(key) || dict[key] == null)
            {
                return fallback;
            }
            if (dict[key] is bool)
            {
                return (bool)dict[key];
            }
            bool parsed;
            if (bool.TryParse(Convert.ToString(dict[key]), out parsed))
            {
                return parsed;
            }
            return fallback;
        }

        public static AppConfig ParseConfig(string json)
        {
            AppConfig cfg = new AppConfig();
            IDictionary<string, object> root = DeserializeObject(json) as IDictionary<string, object>;
            if (root == null)
            {
                return cfg;
            }

            cfg.ApiKey = GetString(root, "apiKey", "");
            cfg.DefaultModel = GetString(root, "defaultModel", "google/gemini-2.5-flash");
            cfg.Models = new List<ModelConfig>();

            object modelsObj;
            if (root.TryGetValue("models", out modelsObj) && modelsObj is IEnumerable)
            {
                foreach (object item in (IEnumerable)modelsObj)
                {
                    IDictionary<string, object> md = item as IDictionary<string, object>;
                    if (md == null) continue;
                    ModelConfig m = new ModelConfig
                    {
                        Id = GetString(md, "id", ""),
                        Name = GetString(md, "name", ""),
                        Provider = GetString(md, "provider", ""),
                        IsVision = GetBool(md, "isVision", false),
                        Status = GetString(md, "status", "untested"),
                        StatusMsg = GetString(md, "statusMsg", ""),
                        TestedAt = GetString(md, "testedAt", "")
                    };
                    if (!string.IsNullOrEmpty(m.Id))
                    {
                        cfg.Models.Add(m);
                    }
                }
            }

            return cfg;
        }

        public static string SerializeConfig(AppConfig cfg)
        {
            List<Dictionary<string, object>> models = new List<Dictionary<string, object>>();
            if (cfg.Models != null)
            {
                foreach (ModelConfig m in cfg.Models)
                {
                    Dictionary<string, object> md = new Dictionary<string, object>();
                    md["id"] = m.Id ?? "";
                    md["name"] = m.Name ?? "";
                    md["provider"] = m.Provider ?? "";
                    md["isVision"] = m.IsVision;
                    md["status"] = string.IsNullOrEmpty(m.Status) ? "verified" : m.Status;
                    if (!string.IsNullOrEmpty(m.StatusMsg))
                    {
                        md["statusMsg"] = m.StatusMsg;
                    }
                    md["testedAt"] = string.IsNullOrEmpty(m.TestedAt)
                        ? DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                        : m.TestedAt;
                    models.Add(md);
                }
            }

            Dictionary<string, object> root = new Dictionary<string, object>();
            root["apiKey"] = cfg.ApiKey ?? "";
            root["defaultModel"] = string.IsNullOrEmpty(cfg.DefaultModel) ? "google/gemini-2.5-flash" : cfg.DefaultModel;
            root["models"] = models;

            return PrettyPrint(SerializeRaw(root));
        }

        public static Project ParseProject(string json)
        {
            IDictionary<string, object> root = DeserializeObject(json) as IDictionary<string, object>;
            if (root == null)
            {
                return null;
            }

            Project p = new Project
            {
                Id = GetString(root, "id", ""),
                Name = GetString(root, "name", "Без названия"),
                Description = GetString(root, "description", ""),
                UpdatedAt = GetString(root, "updatedAt", ""),
                CreatedAt = GetString(root, "createdAt", ""),
                Steps = new List<StepDefinition>()
            };

            object stepsObj;
            if (root.TryGetValue("steps", out stepsObj) && stepsObj is IEnumerable)
            {
                foreach (object item in (IEnumerable)stepsObj)
                {
                    IDictionary<string, object> sd = item as IDictionary<string, object>;
                    if (sd == null) continue;
                    StepDefinition s = new StepDefinition
                    {
                        Id = GetString(sd, "id", ""),
                        Type = GetString(sd, "type", "text"),
                        Title = GetString(sd, "title", ""),
                        Content = GetString(sd, "content", ""),
                        FileName = GetString(sd, "fileName", ""),
                        FileData = GetString(sd, "fileData", ""),
                        FileType = GetString(sd, "fileType", ""),
                        Model = GetString(sd, "model", ""),
                        Prompt = GetString(sd, "prompt", ""),
                        Result = GetString(sd, "result", ""),
                        Status = GetString(sd, "status", "idle"),
                        RequireUser = GetBool(sd, "requireUser", false),
                        UserPrompt = GetString(sd, "userPrompt", ""),
                        Instruction = GetString(sd, "instruction", "")
                    };
                    p.Steps.Add(s);
                }
            }

            return p;
        }

        public static string SerializeProject(Project p)
        {
            List<Dictionary<string, object>> steps = new List<Dictionary<string, object>>();
            if (p.Steps != null)
            {
                foreach (StepDefinition s in p.Steps)
                {
                    Dictionary<string, object> sd = new Dictionary<string, object>();
                    sd["id"] = s.Id ?? "";
                    sd["type"] = string.IsNullOrEmpty(s.Type) ? "text" : s.Type;
                    sd["title"] = s.Title ?? "";
                    sd["content"] = s.Content ?? "";
                    if (!string.IsNullOrEmpty(s.FileName)) sd["fileName"] = s.FileName;
                    if (!string.IsNullOrEmpty(s.FileData)) sd["fileData"] = s.FileData;
                    if (!string.IsNullOrEmpty(s.FileType)) sd["fileType"] = s.FileType;
                    if (!string.IsNullOrEmpty(s.Model)) sd["model"] = s.Model;
                    if (!string.IsNullOrEmpty(s.Prompt)) sd["prompt"] = s.Prompt;
                    if (!string.IsNullOrEmpty(s.Result)) sd["result"] = s.Result;
                    if (!string.IsNullOrEmpty(s.Status)) sd["status"] = s.Status;
                    if (s.RequireUser) sd["requireUser"] = true;
                    if (!string.IsNullOrEmpty(s.UserPrompt)) sd["userPrompt"] = s.UserPrompt;
                    if (!string.IsNullOrEmpty(s.Instruction)) sd["instruction"] = s.Instruction;
                    steps.Add(sd);
                }
            }

            Dictionary<string, object> root = new Dictionary<string, object>();
            root["id"] = p.Id ?? "";
            root["name"] = p.Name ?? "";
            root["description"] = p.Description ?? "";
            root["updatedAt"] = p.UpdatedAt ?? "";
            root["createdAt"] = p.CreatedAt ?? "";
            root["steps"] = steps;

            return PrettyPrint(SerializeRaw(root));
        }
    }
}
