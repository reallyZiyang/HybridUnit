using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Play.Adapters
{
    public partial class API
    {
        public static class Parser
        {
            private static readonly Dictionary<Type, Func<string, object>> Parsers = new();

            static Parser()
            {
                Register(s => s);
                Register(s => byte.TryParse(s, out var v) ? v : (byte)0);
                Register(s => short.TryParse(s, out var v) ? v : (short)0);
                Register(s => int.TryParse(s, out var v) ? v : 0);
                Register(s => long.TryParse(s, out var v) ? v : 0L);
                Register(s => float.TryParse(s, out var v) ? v : 0f);
                Register(s => double.TryParse(s, out var v) ? v : 0.0d);
                Register(s => s.Trim() switch
                {
                    "true" => true,
                    "1" => true,
                    _ => false,
                });
            }

            public static void Register<T>(Func<string, T> parser)
            {
                Parsers[typeof(T)] = s => parser(s);
            }

            public static T Parse<T>(string text, T defaultValue = default)
            {
                try
                {
                    var type = typeof(T);

                    if (Parsers.TryGetValue(type, out var parser))
                        return (T)parser(text);

                    parser = CreateParser(type);
                    Parsers[type] = parser;
                    return (T)parser(text);
                }
                catch
                {
                    return defaultValue;
                }
            }

            public static T ParseArrayItem<T>(string[] arr, int index, T defaultValue = default)
            {
                if (arr == null || index < 0 || index >= arr.Length)
                    return defaultValue;

                return Parse(arr[index], defaultValue);
            }

            private static Func<string, object> CreateParser(Type type)
            {
                if (type.IsEnum)
                {
                    return s =>
                    {
                        try
                        {
                            return Enum.Parse(type, s, ignoreCase: true);
                        }
                        catch
                        {
                            return Activator.CreateInstance(type);
                        }
                    };
                }

                if (type.IsArray)
                {
                    var elementType = type.GetElementType();
                    var elementParser = GetOrCreateParser(elementType);

                    return s =>
                    {
                        if (elementType == null)
                        {
                            Debug.LogError($"[Parser] Unable to determine element type for array Type: {type}");
                            return Array.Empty<object>();
                        }

                        if (string.IsNullOrEmpty(s))
                            return Array.CreateInstance(elementType, 0);

                        var parts = s.Split('|');
                        var arr = Array.CreateInstance(elementType, parts.Length);

                        for (var i = 0; i < parts.Length; i++)
                        {
                            try
                            {
                                arr.SetValue(elementParser(parts[i]), i);
                            }
                            catch
                            {
                                arr.SetValue(GetDefault(elementType), i);
                            }
                        }

                        return arr;
                    };
                }

                Debug.LogWarning($"[DataParser] No parser registered for Type: {type}");
                return _ => GetDefault(type);
            }

            private static Func<string, object> GetOrCreateParser(Type type)
            {
                if (Parsers.TryGetValue(type, out var parser))
                    return parser;

                parser = CreateParser(type);
                Parsers[type] = parser;

                return parser;
            }

            private static object GetDefault(Type type)
            {
                return type.IsValueType ? Activator.CreateInstance(type) : null;
            }

            private static bool ParseBool(string s)
            {
                s = s.Trim().ToLowerInvariant();

                return s switch
                {
                    "true" => true,
                    "1" => true,
                    _ => false,
                };
            }
        }
    }
}