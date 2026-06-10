using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Game.Play.Editor.Systems.Generate
{
    public partial class SystemGenerateEditor
    {
        private const string k_CustomMethods = @"
        #region Custom Methods

        // Add your custom methods here

        #endregion Custom Methods";

        private void WriteProto(OutputOptions opts)
        {
            if (string.IsNullOrEmpty(proto))
                return;
            
            var path = Path.Join(opts.path, "System", $"{opts.name}System.Proto.cs");
            var content = GetContent(opts, "System.Proto", "System", $"{opts.name}System");

            var existingText = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            var protoText = File.ReadAllText(Path.Join(protoPath, proto + ".proto"));

            // 1. 当前协议集合
            var protoSet = specifyProto
                ? protoList.Where(t => t.enable).Select(t => t.name).ToHashSet()
                : ParseS2CProtocol(protoText);
            if (protoSet.Count == 0)
            {
                Debug.Log("No S2C protocol found, skip generating proto handlers.");
                return;
            }

            // 2. 旧代码中已有的方法
            var existingMethods = ParseExistingMethods(existingText);

            var existingSet = existingMethods.Keys.ToHashSet();
            var addSet = protoSet.Except(existingSet).ToList();
            var keepSet = protoSet.Intersect(existingSet).ToList();

            // 3. 生成注册代码（全部按 proto 当前集合重建）
            var callbacks = GenerateCallbacks(protoSet);
            var methodsBuilder = new StringBuilder();

            // 保留旧方法（保留业务逻辑）
            foreach (var item in keepSet)
            {
                methodsBuilder.AppendLine(existingMethods[item]);
                methodsBuilder.AppendLine();
            }

            // 新增方法（空实现）
            foreach (var item in addSet)
            {
                methodsBuilder.AppendLine(GenerateEmptyMethod(item));
                methodsBuilder.AppendLine();
            }

            // ===== 5. 保护 Custom 区 =====
            var customBlock = ExtractCustomBlock(existingText);
            Debug.Log("Extracted Custom Block:\n" + customBlock);
            if (string.IsNullOrEmpty(customBlock))
            {
                customBlock = k_CustomMethods;
            }
            else
            {
                customBlock = "\n\t\t" + customBlock;
            }

            // 6. 写回模板
            content = content
                .Replace("#CALLBACKS#", callbacks.TrimEnd())
                .Replace("#METHODS#", methodsBuilder.ToString().TrimEnd())
                .Replace("#CUSTOM_BLOCK#", customBlock);

            WriteFile(path, content, true);
        }

        /// <summary>
        /// 解析所有 S2C 协议名
        /// </summary>
        private static HashSet<string> ParseS2CProtocol(string text)
        {
            var regex = new Regex(@"^\s*(S2C_[A-Za-z0-9_]+)\s+\d+\s*\{", RegexOptions.Multiline);
            return regex.Matches(text).Select(m => m.Groups[1].Value).ToHashSet();
        }

        // 解析已有方法（保留方法体）
        private static Dictionary<string, string> ParseExistingMethods(string text)
        {
            var dict = new Dictionary<string, string>();

            if (string.IsNullOrEmpty(text))
            {
                return dict;
            }

            var regex = new Regex(
                @"private void (On\w+)\((S2C_[A-Za-z0-9_]+)\.response rsp\)\s*\{[\s\S]*?\}",
                RegexOptions.Multiline);

            foreach (Match m in regex.Matches(text))
            {
                var proto = m.Groups[2].Value;
                dict[proto] = "        " + m.Value.Trim(); // 保留缩进
            }

            return dict;
        }

        private static string ExtractCustomBlock(string text)
        {
            var regex = new Regex(
                @"#region Custom Methods[\s\S]*?#endregion Custom Methods",
                RegexOptions.Multiline);

            var match = regex.Match(text);
            return match.Success ? match.Value.Trim() : "";
        }

        /// <summary>
        /// 生成注册代码
        /// </summary>
        private static string GenerateCallbacks(HashSet<string> list)
        {
            var sb = new StringBuilder();
            foreach (var proto in list)
            {
                var handler = GetHandlerName(proto);
                sb.AppendLine($"\t\t\tthis.RegisterMessageCallback<{proto}.response>({handler});");
            }

            return sb.ToString();
        }

        private static string GenerateEmptyMethod(string proto)
        {
            var handler = GetHandlerName(proto);
            return
                $@"        private void {handler}({proto}.response rsp)
        {{
        }}";
        }

        /// <summary>
        /// S2C_PlayerInfo → OnPlayerInfo
        /// </summary>
        private static string GetHandlerName(string proto)
        {
            return "On" + proto.Substring(4); // 去掉 S2C_
        }
    }
}