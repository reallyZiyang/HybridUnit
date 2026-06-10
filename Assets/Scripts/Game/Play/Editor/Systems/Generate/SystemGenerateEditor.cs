using System;
using System.Collections.Generic;
using System.IO;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace Game.Play.Editor.Systems.Generate
{
    public class OutputOptions
    {
        public string path;
        public string name;
        public string nameSpace;
    }

    public class OutputProto
    {
        [TableColumnWidth(50, Resizable = false)]
        public bool enable;
        [ReadOnly]
        public string name;
    }

    [Flags]
    public enum OutputType
    {
        Model = 1 << 0,
        System = 1 << 1,
    }

    public partial class SystemGenerateEditor : OdinEditorWindow
    {
        [MenuItem("Game/系统生成器", false, 100)]
        private static void OpenWindow()
        {
            var window = GetWindow<SystemGenerateEditor>();
            window.titleContent = new GUIContent("系统生成器");
            window.position = GUIHelper.GetEditorWindowRect().AlignCenter(700, 700);
        }

        protected override void OnEnable()
        {
            InitData();
        }

        private void InitData()
        {
            m_ProtoList.Clear();
            var files = Directory.GetFiles(protoPath, "*.proto", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                m_ProtoList.Add(fileName);
            }
        }

        private readonly List<string> m_ProtoList = new();

        [BoxGroup("设置")] [FolderPath(UseBackslashes = true)]
        public string protoPath = "../game-configs/Protos";

        [BoxGroup("设置")] [FolderPath(ParentFolder = "Assets/Scripts/Game/Play/Runtime/Systems")]
        public string exportPath = "Assets/Scripts/Game/Play/Runtime/Systems";

        [BoxGroup("设置")] [FolderPath(ParentFolder = "Assets/Scripts/Game/Play/Runtime/Systems")]
        public string exportTemplatePath = "Assets/Scripts/Game/Play/Editor/Systems/Generate/Templates";

        [BoxGroup("系统")] [LabelText("命名空间")] public string nameSpace = "Game.Play.Systems";

        [BoxGroup("系统")] [LabelText("系统模块")] [OnValueChanged("UpdateSystemName")]
        public string systemModule;

        [BoxGroup("系统")] [LabelText("指定名称")] public bool specifyName;

        [BoxGroup("系统")] [LabelText("系统名称")] [EnableIf("specifyName")]
        public string systemName;

        private void UpdateSystemName()
        {
            if (specifyName)
            {
                return;
            }

            systemName = systemModule.Split(".")[^1];
        }

        [BoxGroup("协议")] [LabelText("系统协议")] [ValueDropdown("m_ProtoList")] [OnValueChanged("UpdateProto")]
        public string proto = string.Empty;

        [BoxGroup("协议")] [LabelText("指定协议")] [OnValueChanged("UpdateProto")]
        public bool specifyProto;

        [BoxGroup("协议")] [LabelText("协议列表")] [ShowIf("specifyProto")] [TableList] public List<OutputProto> protoList = new();

        private void UpdateProto()
        {
            protoList.Clear();

            if (!specifyProto)
            {
                return;
            }

            if (string.IsNullOrEmpty(proto))
            {
                return;
            }

            var protoText = File.ReadAllText(Path.Join(protoPath, proto + ".proto"));
            var protoSet = ParseS2CProtocol(protoText);
            foreach (var item in protoSet)
            {
                protoList.Add(new OutputProto { enable = false, name = item });
            }
        }

        [BoxGroup("生成")] [LabelText("生成类型")] public OutputType outputType = OutputType.Model | OutputType.System;

        [BoxGroup("生成")]
        [Button("生成")]
        [EnableIf("@this.systemName != null && this.systemName.Length > 0")]
        private void GenerateSystemCode()
        {
            var opts = new OutputOptions()
            {
                path = $"{exportPath}/{systemModule.Replace('.', '/')}",
                name = systemName,
                nameSpace = $"{nameSpace}.{systemModule}",
            };

            if (outputType.HasFlag(OutputType.Model))
            {
                WriteTemplate(opts, "Data", $"{opts.name}Data");
            }

            if (outputType.HasFlag(OutputType.System))
            {
                WriteTemplate(opts, "Interface", $"I{opts.name}System");
                WriteTemplate(opts, "System", $"{opts.name}System");
                WriteProto(opts);
            }

            AssetDatabase.Refresh();
        }

        private void WriteTemplate(OutputOptions opts, string template, string file)
        {
            var content = GetContent(opts, template, template, file);
            var path = Path.Join(opts.path, template, $"{file}.cs");
            WriteFile(path, content);
        }

        private string GetContent(OutputOptions opts, string template, string module, string className)
        {
            var content = GetTemplate($"Sys{template}.txt");
            return content
                .Replace("#USING#", opts.nameSpace)
                .Replace("#NAMESPACE#", $"{opts.nameSpace}.{module}")
                .Replace("#NAME#", className);
        }

        private string GetTemplate(string path)
        {
            return File.ReadAllText(Path.Join(exportTemplatePath, path));
        }

        private static void WriteFile(string path, string content, bool overlay = false)
        {
            if (!overlay && File.Exists(path))
            {
                Debug.LogWarning($"File Exists: {path}");
                return;
            }

            Debug.Log($"Generate File: {path}");

            var dir = Path.GetDirectoryName(path);
            if (dir == null)
            {
                return;
            }

            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(path, content);
        }
    }
}