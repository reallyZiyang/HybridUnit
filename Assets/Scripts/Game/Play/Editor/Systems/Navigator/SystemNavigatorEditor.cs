using System;
using System.Collections.Generic;
using System.Linq;
using Game.Data.Configs.Sys;
using Game.Play.Adapters;
using Game.Play.Systems.Common.Navigator.Data;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UniKit.UI.Core;
using UniKit.UI.Editor.Bindings;
using UnityEditor;
using UnityEngine;

namespace Game.Play.Editor.Systems.Navigator
{
    internal class NavigatorMenu
    {
        public SystemCfg cfg;
        public NavigatorItem item;
        public NavigatorMenu parent;

        private string _path;

        public string path
        {
            get
            {
                if (!string.IsNullOrEmpty(_path))
                    return _path;

                var paths = new List<string>();
                var current = this;
                while (current != null)
                {
                    paths.Add(current.cfg.Name);
                    current = current.parent;
                }

                paths.Reverse();
                _path = string.Join("/", paths);
                return _path;
            }
        }
    }

    internal class SystemNavigatorItemEditor
    {
        private static NavigatorData s_Data;
        private static List<UIDataBinding> s_UITypes;
        private static readonly List<NavigatorMenu> k_AllMenus = new();
        private static readonly Dictionary<int, NavigatorMenu> k_MenuDict = new();

        public static void Init(NavigatorData data)
        {
            s_Data = data;
            k_AllMenus.Clear();
            k_MenuDict.Clear();
        }

        private readonly NavigatorMenu m_Menu;
        private readonly NavigatorItem m_Item;

        public SystemNavigatorItemEditor(NavigatorMenu menu)
        {
            m_Menu = menu;
            m_Item = menu.item;
            k_AllMenus.Add(menu);
            k_MenuDict.Add(menu.item.id, menu);
        }

        [ShowInInspector] [ReadOnly] public int ID => m_Item.id;

        [ShowInInspector] [ReadOnly] public SystemType Type => (SystemType)m_Item.id;

        [ShowInInspector]
        [ValueDropdown(nameof(GetParents))]
        [LabelText("Parent")]
        public int Parent
        {
            get => m_Item.parent;
            set
            {
                if (value == m_Item.parent) return;
                m_Item.parent = value;
                m_Menu.parent = k_MenuDict.GetValueOrDefault(value);
                Refresh();
            }
        }

        [ShowInInspector]
        public int Sort
        {
            get => m_Item.sort;
            set
            {
                if (m_Item.sort == value) return;
                m_Item.sort = value;
                Refresh();
            }
        }

        [ShowInInspector]
        public bool Skippable
        {
            get => m_Item.skippable;
            set
            {
                m_Item.skippable = value;
                Save();
            }
        }

        [ShowInInspector]
        [ValueDropdown(nameof(GetUITypes))]
        [LabelText("UI Binding")]
        public UIDataBinding UI
        {
            get => m_Item.binding;
            set
            {
                if (m_Item.binding == value) return;
                m_Item.binding = value;
                m_Item.type = UIDataBindingUtilities.GetPrefabModulePath(value.gameObject);
                m_Item.jumpType = m_Item.jumpType == JumpType.None ? JumpType.Open : m_Item.jumpType;
                Refresh();
            }
        }

        [ShowInInspector]
        [ReadOnly]
        [LabelText("UI Path")]
        public string UIType
        {
            get => m_Item.type;
            set => m_Item.type = value;
        }

        [ShowInInspector]
        [LabelText("UI Jump Type")]
        public JumpType JumpType
        {
            get => m_Item.jumpType;
            set
            {
                if (m_Item.jumpType == value) return;
                m_Item.jumpType = value;
                Save();
            }
        }

        private IEnumerable<ValueDropdownItem<int>> GetParents()
        {
            yield return new ValueDropdownItem<int>("None", 0);

            foreach (var node in k_AllMenus)
            {
                var label = $"{node.cfg.Name} ({node.item.id})";
                yield return new ValueDropdownItem<int>(label, node.item.id);
            }
        }

        private IEnumerable<UIDataBinding> GetUITypes()
        {
            s_UITypes ??= EditorUtilities.FindPrefabsWithScript<UIDataBinding>()
                .Where(binding => binding != null && binding.type == UniKit.UI.Core.UIType.View)
                .OrderBy(binding => binding.name)
                .ToList();

            return s_UITypes;
        }

        private static void Refresh()
        {
            Save();
            EditorWindow.GetWindow<SystemNavigatorEditor>()?.ForceMenuTreeRebuild();
        }

        private static void Save()
        {
            EditorUtility.SetDirty(s_Data);
            AssetDatabase.SaveAssets();
        }
    }

    public class SystemNavigatorEditor : OdinMenuEditorWindow
    {
        private const string k_NavigatorDataKey = "sys_tbsystem";

        private static TbSystem s_TbSystem;
        private static NavigatorData s_NavData;

        [MenuItem("Game/系统导航", false, 100)]
        private static void OpenWindow()
        {
            var window = GetWindow<SystemNavigatorEditor>();
            window.titleContent = new GUIContent("系统导航");
            window.Show();
        }

        protected override OdinMenuTree BuildMenuTree()
        {
            SystemNavigatorItemEditor.Init(s_NavData);

            var menus = new List<NavigatorMenu>();
            var menuDic = new Dictionary<int, NavigatorMenu>();
            foreach (var item in s_NavData.items)
            {
                var menu = new NavigatorMenu()
                {
                    cfg = s_TbSystem.GetOrDefault((SystemType)item.id),
                    item = item,
                };
                menus.Add(menu);
                menuDic[item.id] = menu;
            }

            foreach (var menu in menus)
            {
                if (menu.item.parent != 0 && menuDic.TryGetValue(menu.item.parent, out var parentMenu))
                {
                    menu.parent = parentMenu;
                }
            }

            var tree = new OdinMenuTree(supportsMultiSelect: false)
            {
                Config = { DrawSearchToolbar = true }
            };

            foreach (var menu in GetSortedMenus(menus))
            {
                tree.Add(menu.path, new SystemNavigatorItemEditor(menu));
            }

            return tree;
        }

        private static IEnumerable<NavigatorMenu> GetSortedMenus(List<NavigatorMenu> menus)
        {
            var childrenMap = menus
                .Where(menu => menu.parent != null)
                .GroupBy(menu => menu.parent.item.id)
                .ToDictionary(group => group.Key, group => group.ToList());

            foreach (var menu in menus.Where(menu => menu.parent == null))
            {
                foreach (var sortedMenu in TraverseMenus(menu, childrenMap))
                {
                    yield return sortedMenu;
                }
            }
        }

        private static IEnumerable<NavigatorMenu> TraverseMenus(NavigatorMenu menu,
            IReadOnlyDictionary<int, List<NavigatorMenu>> childrenMap)
        {
            yield return menu;

            if (!childrenMap.TryGetValue(menu.item.id, out var children))
            {
                yield break;
            }

            foreach (var child in SortMenus(children, childrenMap))
            {
                foreach (var sortedMenu in TraverseMenus(child, childrenMap))
                {
                    yield return sortedMenu;
                }
            }
        }

        private static IEnumerable<NavigatorMenu> SortMenus(IEnumerable<NavigatorMenu> menus,
            IReadOnlyDictionary<int, List<NavigatorMenu>> childrenMap)
        {
            return menus
                .OrderBy(menu => childrenMap.ContainsKey(menu.item.id))
                .ThenBy(menu => menu.item.sort)
                .ThenBy(menu => menu.item.id);
        }

        protected override void OnEnable()
        {
            InitData();
        }

        private void InitData()
        {
            s_NavData ??= EditorUtilities.FindScriptableObject<NavigatorData>();
            s_TbSystem ??= API.LoadConfig<TbSystem>(k_NavigatorDataKey);

            var isDirty = RemoveDuplicateItems();
            var existingIds = new HashSet<SystemType>();
            var notExistingIds = new HashSet<int>();

            foreach (var item in s_NavData.items)
            {
                if (Enum.IsDefined(typeof(SystemType), item.id))
                {
                    existingIds.Add((SystemType)item.id);
                }
                else
                {
                    notExistingIds.Add(item.id);
                }
            }

            if (notExistingIds.Count > 0)
            {
                s_NavData.items.RemoveAll(item => notExistingIds.Contains(item.id));
                isDirty = true;
            }

            var newItems = s_TbSystem.DataList.Where(item => !existingIds.Contains(item.Id))
                .Select(item => new NavigatorItem { id = (int)item.Id, }).ToList();

            if (newItems.Count > 0)
            {
                s_NavData.items.AddRange(newItems);
                isDirty = true;
            }

            if (!isDirty) return;

            EditorUtility.SetDirty(s_NavData);
            AssetDatabase.SaveAssets();
        }

        private static bool RemoveDuplicateItems()
        {
            var duplicateItems = s_NavData.items
                .GroupBy(item => item.id)
                .Where(group => group.Count() > 1)
                .SelectMany(group => group
                    .OrderBy(item => item.jumpType == JumpType.None)
                    .Skip(1))
                .ToHashSet();

            if (duplicateItems.Count <= 0)
            {
                return false;
            }

            s_NavData.items.RemoveAll(duplicateItems.Contains);
            return true;
        }
    }
}