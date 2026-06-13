using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Game.Play.Editor.UI
{
    public class UIFontLibraryGenerator : EditorWindow
    {
        private const string DefaultSourceFontPath = "C:/Windows/Fonts/NotoSansSC-VF.ttf";
        private const string FontOutputFolder = "Assets/Res/UI/Common/Font";
        private const string SourceFontFolder = FontOutputFolder + "/Source";
        private const string DefaultOutputName = "UI_Common_TMP";
        private const string DefaultCustomCharsPath = FontOutputFolder + "/UI_Custom_Chars.txt";
        private const string DefaultCharsetPath = FontOutputFolder + "/UI_Common_Chars.txt";
        private const string GeneratedCharsetPath = FontOutputFolder + "/UI_Common_Chars.generated.txt";

        private static readonly string[] ScanRoots =
        {
            "Assets/Scripts/Game/Play/Runtime/UI",
            "Assets/Res/UI",
        };

        private static readonly HashSet<string> ScanExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".cs",
            ".prefab",
            ".asset",
            ".txt",
            ".json",
            ".yaml",
            ".yml",
        };

        private string m_SourceFontPath = DefaultSourceFontPath;
        private string m_OutputName = DefaultOutputName;
        private int m_AtlasSize = 4096;
        private int m_SamplingPointSize = 90;
        private int m_Padding = 9;
        private GlyphRenderMode m_RenderMode = GlyphRenderMode.SDFAA;
        private ChineseCharacterSetPreset m_ChineseCharacterSet = ChineseCharacterSetPreset.DefaultFile;
        private bool m_ScanProjectText = true;
        private bool m_OverwriteExisting;
        private TextAsset m_CharsetAsset;
        private TextAsset m_CustomCharsAsset;
        private Vector2 m_Scroll;
        private string m_LastReport = string.Empty;

        [MenuItem("Game/UI/字体库生成器", false, 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<UIFontLibraryGenerator>();
            window.titleContent = new GUIContent("字体库生成器");
            window.minSize = new Vector2(540, 520);
            window.Focus();
        }

        public static void GenerateDefault()
        {
            var options = GenerationOptions.CreateDefault();
            var success = Generate(options, out var report);

            if (!success)
            {
                Debug.LogError(report);
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }

                return;
            }

            Debug.Log(report);
        }

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(m_SourceFontPath))
            {
                m_SourceFontPath = DefaultSourceFontPath;
            }

            m_CharsetAsset ??= AssetDatabase.LoadAssetAtPath<TextAsset>(DefaultCharsetPath);
        }

        private void OnGUI()
        {
            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Source Font", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                m_SourceFontPath = EditorGUILayout.TextField("Font File", m_SourceFontPath);
                if (GUILayout.Button("Select", GUILayout.Width(80)))
                {
                    SelectSourceFont();
                }
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            m_OutputName = EditorGUILayout.TextField("Asset Name", m_OutputName);
            EditorGUILayout.LabelField("Folder", FontOutputFolder);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Atlas", EditorStyles.boldLabel);
            m_AtlasSize = EditorGUILayout.IntPopup("Atlas Size", m_AtlasSize, new[] { "1024", "2048", "4096", "8192" }, new[] { 1024, 2048, 4096, 8192 });
            m_SamplingPointSize = EditorGUILayout.IntField("Sampling Point Size", m_SamplingPointSize);
            m_Padding = EditorGUILayout.IntField("Padding", m_Padding);
            m_RenderMode = (GlyphRenderMode)EditorGUILayout.EnumPopup("Render Mode", m_RenderMode);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Characters", EditorStyles.boldLabel);
            m_ChineseCharacterSet = (ChineseCharacterSetPreset)EditorGUILayout.IntPopup(
                "中文字符集",
                (int)m_ChineseCharacterSet,
                new[] { "字符集文件", "常用 UI 中文", "GB2312 一级常用汉字", "GB2312 全集" },
                new[] { (int)ChineseCharacterSetPreset.DefaultFile, (int)ChineseCharacterSetPreset.CommonUiChinese, (int)ChineseCharacterSetPreset.Gb2312Level1, (int)ChineseCharacterSetPreset.Gb2312All });
            using (new EditorGUI.DisabledScope(m_ChineseCharacterSet != ChineseCharacterSetPreset.DefaultFile))
            {
                m_CharsetAsset = (TextAsset)EditorGUILayout.ObjectField("Character Set File", m_CharsetAsset, typeof(TextAsset), false);
            }

            m_ScanProjectText = EditorGUILayout.Toggle("Scan Project UI Text", m_ScanProjectText);
            m_CustomCharsAsset = (TextAsset)EditorGUILayout.ObjectField("Custom Characters", m_CustomCharsAsset, typeof(TextAsset), false);
            EditorGUILayout.LabelField("Default Character Set", DefaultCharsetPath);
            EditorGUILayout.LabelField("Default Custom File", DefaultCustomCharsPath);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Safety", EditorStyles.boldLabel);
            m_OverwriteExisting = EditorGUILayout.Toggle("Overwrite Existing Asset", m_OverwriteExisting);

            EditorGUILayout.Space(14);
            using (new EditorGUI.DisabledScope(!CanGenerateFromWindow()))
            {
                if (GUILayout.Button("Generate TMP Font Library", GUILayout.Height(32)))
                {
                    GenerateFromWindow();
                }
            }

            if (!string.IsNullOrWhiteSpace(m_LastReport))
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Last Report", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(m_LastReport, MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        private void SelectSourceFont()
        {
            var initialDirectory = File.Exists(m_SourceFontPath)
                ? Path.GetDirectoryName(m_SourceFontPath)
                : Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

            var path = EditorUtility.OpenFilePanelWithFilters(
                "Select Source Font",
                initialDirectory,
                new[] { "Font files", "ttf,otf,ttc", "All files", "*" });

            if (!string.IsNullOrEmpty(path))
            {
                m_SourceFontPath = path.Replace('\\', '/');
            }
        }

        private bool CanGenerateFromWindow()
        {
            return !string.IsNullOrWhiteSpace(m_SourceFontPath)
                   && !string.IsNullOrWhiteSpace(m_OutputName)
                   && m_AtlasSize > 0
                   && m_SamplingPointSize > 0
                   && m_Padding >= 0;
        }

        private void GenerateFromWindow()
        {
            var options = new GenerationOptions
            {
                sourceFontPath = m_SourceFontPath,
                outputName = m_OutputName,
                atlasSize = m_AtlasSize,
                samplingPointSize = m_SamplingPointSize,
                padding = m_Padding,
                renderMode = m_RenderMode,
                chineseCharacterSet = m_ChineseCharacterSet,
                charsetAssetPath = GetAssetPath(m_CharsetAsset),
                scanProjectText = m_ScanProjectText,
                overwriteExisting = m_OverwriteExisting,
                customCharsAssetPath = GetAssetPath(m_CustomCharsAsset),
            };

            var success = Generate(options, out m_LastReport);
            if (success)
            {
                Debug.Log(m_LastReport);
                EditorUtility.DisplayDialog("字体库生成器", "字体库生成完成。", "OK");
            }
            else
            {
                Debug.LogError(m_LastReport);
                EditorUtility.DisplayDialog("字体库生成器", "字体库生成失败或存在缺字，请查看 Console 和 missing report。", "OK");
            }
        }

        private static bool Generate(GenerationOptions options, out string report)
        {
            var messages = new List<string>();

            if (!ValidateOptions(options, messages))
            {
                report = string.Join(Environment.NewLine, messages);
                return false;
            }

            EnsureAssetFolder(FontOutputFolder);
            EnsureAssetFolder(SourceFontFolder);

            if (!PrepareSourceFont(options, messages, out var sourceFontAssetPath, out var sourceFont))
            {
                report = string.Join(Environment.NewLine, messages);
                return false;
            }

            var characters = BuildCharacterSet(options, messages);
            WriteTextAsset(GeneratedCharsetPath, characters);

            var fontAssetPath = $"{FontOutputFolder}/{options.outputName}.asset";
            var missingReportPath = $"{FontOutputFolder}/{options.outputName}.missing.txt";

            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontAssetPath) && !options.overwriteExisting)
            {
                messages.Add($"Font asset already exists: {fontAssetPath}");
                messages.Add("Enable overwrite in the window before replacing it.");
                report = string.Join(Environment.NewLine, messages);
                return false;
            }

            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(fontAssetPath))
            {
                AssetDatabase.DeleteAsset(fontAssetPath);
            }

            var fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                options.samplingPointSize,
                options.padding,
                options.renderMode,
                options.atlasSize,
                options.atlasSize,
                AtlasPopulationMode.Dynamic,
                false);

            if (!fontAsset)
            {
                messages.Add($"Failed to create TMP font asset from: {sourceFontAssetPath}");
                report = string.Join(Environment.NewLine, messages);
                return false;
            }

            fontAsset.name = options.outputName;
            NameSubAssets(fontAsset, options.outputName);

            AssetDatabase.CreateAsset(fontAsset, fontAssetPath);
            AddSubAssets(fontAsset);

            var allCharactersAdded = fontAsset.TryAddCharacters(characters, out var missingCharacters, false);
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;

            EditorUtility.SetDirty(fontAsset);
            MarkSubAssetsDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(fontAssetPath, ImportAssetOptions.ForceUpdate);

            WriteMissingReport(missingReportPath, missingCharacters, characters.Length, options, sourceFontAssetPath);

            messages.Add($"Source font: {sourceFontAssetPath}");
            messages.Add($"Character set: {GeneratedCharsetPath} ({characters.Length} chars)");
            messages.Add($"Font asset: {fontAssetPath}");
            messages.Add($"Missing report: {missingReportPath}");

            if (!allCharactersAdded)
            {
                messages.Add($"Missing characters: {missingCharacters.Length}");
                messages.Add("Increase atlas size, reduce character set, or choose another source font.");
                report = string.Join(Environment.NewLine, messages);
                return false;
            }

            messages.Add("No missing characters.");
            report = string.Join(Environment.NewLine, messages);
            return true;
        }

        private static bool ValidateOptions(GenerationOptions options, List<string> messages)
        {
            if (string.IsNullOrWhiteSpace(options.sourceFontPath))
            {
                messages.Add("Source font path is empty.");
                return false;
            }

            var sourcePath = NormalizePath(options.sourceFontPath);
            if (!File.Exists(sourcePath))
            {
                messages.Add($"Source font file does not exist: {sourcePath}");
                return false;
            }

            var extension = Path.GetExtension(sourcePath);
            if (!IsSupportedFontExtension(extension))
            {
                messages.Add($"Unsupported font extension: {extension}");
                messages.Add("Supported extensions: .ttf, .otf, .ttc");
                return false;
            }

            if (string.IsNullOrWhiteSpace(options.outputName))
            {
                messages.Add("Output name is empty.");
                return false;
            }

            if (options.outputName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || options.outputName.Contains('/') || options.outputName.Contains('\\'))
            {
                messages.Add($"Output name contains invalid file name characters: {options.outputName}");
                return false;
            }

            if (options.atlasSize <= 0 || options.samplingPointSize <= 0 || options.padding < 0)
            {
                messages.Add("Atlas size and sampling point size must be positive; padding cannot be negative.");
                return false;
            }

            return true;
        }

        private static bool PrepareSourceFont(GenerationOptions options, List<string> messages, out string sourceFontAssetPath, out Font sourceFont)
        {
            var sourceFullPath = NormalizePath(options.sourceFontPath);
            var sourceFileName = Path.GetFileName(sourceFullPath);
            var targetAssetPath = $"{SourceFontFolder}/{sourceFileName}";
            var targetFullPath = AssetPathToFullPath(targetAssetPath);
            var sourceAssetPath = TryGetProjectAssetPath(sourceFullPath);

            if (!string.Equals(sourceAssetPath, targetAssetPath, StringComparison.OrdinalIgnoreCase))
            {
                var shouldCopy = options.overwriteExisting || !File.Exists(targetFullPath);
                if (shouldCopy)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(targetFullPath));
                    File.Copy(sourceFullPath, targetFullPath, true);
                    messages.Add($"Copied source font to: {targetAssetPath}");
                }
                else
                {
                    messages.Add($"Using existing copied source font: {targetAssetPath}");
                }

                sourceFontAssetPath = targetAssetPath;
            }
            else
            {
                sourceFontAssetPath = sourceAssetPath;
                messages.Add($"Using project source font: {sourceFontAssetPath}");
            }

            AssetDatabase.ImportAsset(sourceFontAssetPath, ImportAssetOptions.ForceUpdate);

            if (AssetImporter.GetAtPath(sourceFontAssetPath) is TrueTypeFontImporter importer)
            {
                if (!importer.includeFontData)
                {
                    importer.includeFontData = true;
                    importer.SaveAndReimport();
                }
            }

            sourceFont = AssetDatabase.LoadAssetAtPath<Font>(sourceFontAssetPath);
            if (!sourceFont)
            {
                messages.Add($"Unity could not load the source font as a Font asset: {sourceFontAssetPath}");
                return false;
            }

            return true;
        }

        private static string BuildCharacterSet(GenerationOptions options, List<string> messages)
        {
            var characters = new SortedSet<char>();
            AddVisibleAscii(characters);
            AddString(characters, CommonPunctuation);
            AddChineseCharacters(characters, options, messages);

            if (options.scanProjectText)
            {
                var scannedFiles = ScanProjectText(characters);
                messages.Add($"Scanned UI text files: {scannedFiles}");
            }

            if (File.Exists(AssetPathToFullPath(DefaultCustomCharsPath)))
            {
                AddString(characters, File.ReadAllText(AssetPathToFullPath(DefaultCustomCharsPath), Encoding.UTF8));
                messages.Add($"Merged default custom characters: {DefaultCustomCharsPath}");
            }

            if (!string.IsNullOrEmpty(options.customCharsAssetPath) && options.customCharsAssetPath != DefaultCustomCharsPath)
            {
                var customText = AssetDatabase.LoadAssetAtPath<TextAsset>(options.customCharsAssetPath);
                if (customText)
                {
                    AddString(characters, customText.text);
                    messages.Add($"Merged selected custom characters: {options.customCharsAssetPath}");
                }
            }

            return new string(characters.ToArray());
        }

        private static void AddChineseCharacters(SortedSet<char> characters, GenerationOptions options, List<string> messages)
        {
            if (options.chineseCharacterSet != ChineseCharacterSetPreset.DefaultFile)
            {
                AddChinesePreset(characters, options.chineseCharacterSet, messages);
                return;
            }

            var charsetPath = string.IsNullOrEmpty(options.charsetAssetPath) ? DefaultCharsetPath : options.charsetAssetPath;
            var fullPath = AssetPathToFullPath(charsetPath);
            if (!File.Exists(fullPath))
            {
                messages.Add($"Default character set file was not found, fallback to Common UI Chinese: {charsetPath}");
                AddChinesePreset(characters, ChineseCharacterSetPreset.CommonUiChinese, messages);
                return;
            }

            AddString(characters, File.ReadAllText(fullPath, Encoding.UTF8));
            messages.Add($"Chinese character set: File ({charsetPath})");
        }

        private static void AddChinesePreset(SortedSet<char> characters, ChineseCharacterSetPreset preset, List<string> messages)
        {
            AddString(characters, CommonUiChinese);
            AddString(characters, CommonUiTerms);

            if (preset == ChineseCharacterSetPreset.CommonUiChinese)
            {
                messages.Add("Chinese character set: Common UI Chinese");
                return;
            }

            var beforeCount = characters.Count;
            var includeLevel2 = preset == ChineseCharacterSetPreset.Gb2312All;
            AddGb2312Characters(characters, includeLevel2, messages);
            var addedCount = characters.Count - beforeCount;

            messages.Add(includeLevel2
                ? $"Chinese character set: GB2312 all (+{addedCount} chars)"
                : $"Chinese character set: GB2312 level 1 (+{addedCount} chars)");
        }

        private static void AddGb2312Characters(SortedSet<char> characters, bool includeLevel2, List<string> messages)
        {
            Encoding encoding;
            try
            {
                encoding = Encoding.GetEncoding("GB2312");
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException)
            {
                messages.Add($"GB2312 encoding is unavailable in this Unity runtime: {ex.Message}");
                return;
            }

            var endLeadByte = includeLevel2 ? 0xF7 : 0xD7;
            for (var leadByte = 0xB0; leadByte <= endLeadByte; leadByte++)
            {
                for (var trailByte = 0xA1; trailByte <= 0xFE; trailByte++)
                {
                    var decoded = encoding.GetString(new[] { (byte)leadByte, (byte)trailByte });
                    if (string.IsNullOrEmpty(decoded))
                    {
                        continue;
                    }

                    foreach (var c in decoded)
                    {
                        if (c == '?' || char.IsControl(c) || char.IsSurrogate(c))
                        {
                            continue;
                        }

                        characters.Add(c);
                    }
                }
            }
        }

        private static int ScanProjectText(SortedSet<char> characters)
        {
            var scannedFiles = 0;
            foreach (var root in ScanRoots)
            {
                var fullRoot = AssetPathToFullPath(root);
                if (!Directory.Exists(fullRoot))
                {
                    continue;
                }

                foreach (var file in Directory.EnumerateFiles(fullRoot, "*.*", SearchOption.AllDirectories))
                {
                    if (!ScanExtensions.Contains(Path.GetExtension(file)))
                    {
                        continue;
                    }

                    try
                    {
                        AddString(characters, File.ReadAllText(file, Encoding.UTF8));
                        scannedFiles++;
                    }
                    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is DecoderFallbackException)
                    {
                        Debug.LogWarning($"Skip UI text scan file: {file}\n{ex.Message}");
                    }
                }
            }

            return scannedFiles;
        }

        private static void AddVisibleAscii(SortedSet<char> characters)
        {
            for (var c = ' '; c <= '~'; c++)
            {
                characters.Add(c);
            }
        }

        private static void AddString(SortedSet<char> characters, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            foreach (var c in value)
            {
                if (char.IsControl(c) || char.IsSurrogate(c))
                {
                    continue;
                }

                characters.Add(c);
            }
        }

        private static void NameSubAssets(TMP_FontAsset fontAsset, string outputName)
        {
            if (fontAsset.atlasTextures != null)
            {
                for (var i = 0; i < fontAsset.atlasTextures.Length; i++)
                {
                    if (fontAsset.atlasTextures[i])
                    {
                        fontAsset.atlasTextures[i].name = i == 0 ? $"{outputName} Atlas" : $"{outputName} Atlas {i}";
                    }
                }
            }

            if (fontAsset.material)
            {
                fontAsset.material.name = $"{outputName} Material";
            }
        }

        private static void AddSubAssets(TMP_FontAsset fontAsset)
        {
            if (fontAsset.atlasTextures != null)
            {
                foreach (var texture in fontAsset.atlasTextures)
                {
                    if (texture && !AssetDatabase.Contains(texture))
                    {
                        AssetDatabase.AddObjectToAsset(texture, fontAsset);
                    }
                }
            }

            if (fontAsset.material && !AssetDatabase.Contains(fontAsset.material))
            {
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }
        }

        private static void MarkSubAssetsDirty(TMP_FontAsset fontAsset)
        {
            if (fontAsset.atlasTextures != null)
            {
                foreach (var texture in fontAsset.atlasTextures)
                {
                    if (texture)
                    {
                        EditorUtility.SetDirty(texture);
                    }
                }
            }

            if (fontAsset.material)
            {
                EditorUtility.SetDirty(fontAsset.material);
            }
        }

        private static void WriteMissingReport(string assetPath, string missingCharacters, int requestedCount, GenerationOptions options, string sourceFontAssetPath)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Source Font: {sourceFontAssetPath}");
            builder.AppendLine($"Output Name: {options.outputName}");
            builder.AppendLine($"Requested Characters: {requestedCount}");
            builder.AppendLine($"Atlas Size: {options.atlasSize}x{options.atlasSize}");
            builder.AppendLine($"Sampling Point Size: {options.samplingPointSize}");
            builder.AppendLine($"Padding: {options.padding}");
            builder.AppendLine($"Render Mode: {options.renderMode}");
            builder.AppendLine();

            if (string.IsNullOrEmpty(missingCharacters))
            {
                builder.AppendLine("Missing Characters: 0");
            }
            else
            {
                builder.AppendLine($"Missing Characters: {missingCharacters.Length}");
                foreach (var c in missingCharacters)
                {
                    builder.AppendLine($"U+{(int)c:X4} {c}");
                }
            }

            WriteTextAsset(assetPath, builder.ToString());
        }

        private static void WriteTextAsset(string assetPath, string content)
        {
            var fullPath = AssetPathToFullPath(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, content, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        private static void EnsureAssetFolder(string assetFolder)
        {
            var fullPath = AssetPathToFullPath(assetFolder);
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
            }

            AssetDatabase.ImportAsset(assetFolder, ImportAssetOptions.ForceUpdate);
        }

        private static string TryGetProjectAssetPath(string fullOrAssetPath)
        {
            var rawPath = fullOrAssetPath.Replace('\\', '/');
            if (rawPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) || string.Equals(rawPath, "Assets", StringComparison.OrdinalIgnoreCase))
            {
                return rawPath;
            }

            var normalized = NormalizePath(fullOrAssetPath);
            var projectRoot = NormalizePath(Directory.GetCurrentDirectory());
            if (!normalized.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return normalized.Substring(projectRoot.Length + 1);
        }

        private static string AssetPathToFullPath(string assetPath)
        {
            var projectRoot = NormalizePath(Directory.GetCurrentDirectory());
            return NormalizePath(Path.Combine(projectRoot, assetPath));
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : Path.GetFullPath(path).Replace('\\', '/');
        }

        private static string GetAssetPath(UnityEngine.Object asset)
        {
            return asset ? AssetDatabase.GetAssetPath(asset) : null;
        }

        private static bool IsSupportedFontExtension(string extension)
        {
            return string.Equals(extension, ".ttf", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(extension, ".otf", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(extension, ".ttc", StringComparison.OrdinalIgnoreCase);
        }

        private sealed class GenerationOptions
        {
            public string sourceFontPath;
            public string outputName;
            public int atlasSize;
            public int samplingPointSize;
            public int padding;
            public GlyphRenderMode renderMode;
            public ChineseCharacterSetPreset chineseCharacterSet;
            public string charsetAssetPath;
            public bool scanProjectText;
            public bool overwriteExisting;
            public string customCharsAssetPath;

            public static GenerationOptions CreateDefault()
            {
                return new GenerationOptions
                {
                    sourceFontPath = DefaultSourceFontPath,
                    outputName = DefaultOutputName,
                    atlasSize = 4096,
                    samplingPointSize = 90,
                    padding = 9,
                    renderMode = GlyphRenderMode.SDFAA,
                    chineseCharacterSet = ChineseCharacterSetPreset.DefaultFile,
                    charsetAssetPath = DefaultCharsetPath,
                    scanProjectText = true,
                    overwriteExisting = false,
                };
            }
        }

        private enum ChineseCharacterSetPreset
        {
            DefaultFile = 0,
            CommonUiChinese = 1,
            Gb2312Level1 = 2,
            Gb2312All = 3,
        }

        private const string CommonPunctuation =
            "，。！？；：、（）【】《》“”‘’…—-·～￥%#@&*/+=<>[]{}|\\\"'`^_.,!?;:()";

        private const string CommonUiChinese =
            "的一是在不了有和人这中大为上个国我以要他时来用们生到作地于出就分对成会可主发年动同工也能下过子说产种面而方后多定行学法所民得经十三之进着等部度家电力里如水化高自二理起小物现实加量都两体制机当使点从业本去把性好应开它合还因由其些然前外天政四日那社义事平形相全表间样与关各重新线内数正心反你明看原又么利比或但质气第向道命此变条只没结解问意建月公无系军很情者最立代想已通并提直题程展五果料象员革位入常文总次品式活设及管特件长求老头基资边流路级少图山统接知较将组见计别她手角期根论运农指几九区强放决西被干做必战先回则任取据处队南给色光门即保治北造百规热领七海口东导器压志世金增争济阶油思术极交受联什认六共权收证改清美再采转更单风切打白教速花带安场身车例真务具万每目至达走积示议声报斗完类八离华名确才科张信马节话米整空元况今集温传土许步群广石记需段研界拉林律叫且究观越织装影算低持音众书布复容儿须际商非验连断深难近矿千周委素技备半办青省列习响约支般史感劳便团往酸历市克何除消构府称太准精值号率族维划选标写存候毛亲快效斯院查江型眼王按格养易置派层片始却专状育厂京识适属圆包火住调满县局照参红细引听该铁价严龙飞";

        private const string CommonUiTerms =
            "开始暂停继续退出返回确认取消关闭打开设置保存加载登录注册购买出售升级强化装备背包角色英雄关卡任务奖励领取挑战战斗胜利失败生命魔法攻击防御速度经验金币钻石体力等级排行榜邮件好友商城活动公告提示错误网络连接重试刷新跳过下一步上一步上一页下一页数量价格不足已满未解锁免费付费每日每周限时新手引导服务器账号密码头像昵称队伍阵容技能天赋属性品质稀有普通优秀精良史诗传说神话红点领取完成未完成进行中自动手动跳转详情说明帮助目标奖励惩罚冷却时间消耗获得拥有剩余最大最小全部筛选排序搜索输入输出已装备未装备穿戴卸下合成分解兑换领取一键分享复制粘贴";
    }
}
