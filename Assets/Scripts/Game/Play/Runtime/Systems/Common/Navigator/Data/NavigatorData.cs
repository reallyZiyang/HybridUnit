using System;
using System.Collections.Generic;
using UniKit.UI.Core;
using UnityEngine;

namespace Game.Play.Systems.Common.Navigator.Data
{
    [CreateAssetMenu(fileName = "Navigator Data", menuName = "Game/Navigator Data")]
    public class NavigatorData : ScriptableObject
    {
        [SerializeField] public List<NavigatorItem> items;
    }

    [Serializable]
    public class NavigatorItem
    {
        public int id;
        public int parent;
        public int sort;
        public string type;
        public JumpType jumpType;
        public string jumpParam;
        public bool skippable;

#if UNITY_EDITOR
        public UIDataBinding binding;
#endif
    }

    public enum JumpType
    {
        None,
        Open,
        URL,
    }
}