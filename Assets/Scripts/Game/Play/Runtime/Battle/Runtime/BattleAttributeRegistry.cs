using System;
using System.Collections.Generic;
using Game.Data.Configs.Attr;

namespace Game.Play.Battle.Runtime
{
    public readonly struct BattleAttributeValue
    {
        public readonly AttributeType type;
        public readonly long value;

        public BattleAttributeValue(AttributeType type, long value)
        {
            this.type = type;
            this.value = value;
        }
    }

    public static class BattleAttributeRegistry
    {
        private static readonly AttributeType[] sTypes;
        private static readonly Dictionary<AttributeType, int> sIndexByType;

        static BattleAttributeRegistry()
        {
            Array values = Enum.GetValues(typeof(AttributeType));
            List<AttributeType> types = new(values.Length);
            sIndexByType = new Dictionary<AttributeType, int>(values.Length);

            foreach (object value in values)
            {
                AttributeType type = (AttributeType)value;
                if (type == AttributeType.Null || sIndexByType.ContainsKey(type))
                {
                    continue;
                }

                sIndexByType.Add(type, types.Count);
                types.Add(type);
            }

            sTypes = types.ToArray();
        }

        public static int Count => sTypes.Length;

        public static bool TryGetIndex(AttributeType type, out int index)
        {
            return sIndexByType.TryGetValue(type, out index);
        }
    }
}
