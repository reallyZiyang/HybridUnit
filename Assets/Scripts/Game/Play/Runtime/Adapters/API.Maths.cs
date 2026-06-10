using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Cysharp.Threading.Tasks;
using Naipa.UIToolbar.Palette;
using UnityEngine;

namespace Game.Play.Adapters
{
    public static partial class API
    {
        public struct Maths
        {
            public enum SchemeType
            {
                Standard,
                Currency,
                Power,
            }

            private static readonly Dictionary<SchemeType, (long overValue, long divisor, string suffix)[]> Schemes =
                new()
                {
                    {
                        SchemeType.Standard,
                        new[]
                        {
                            (1L, 1L, ""), // 1-6位: 直接显示
                            (1000000L, 10000000L, "万"), // 7-8位
                            (100000000L, 100000000000L, "亿")
                        }
                    },
                    {
                        SchemeType.Currency,
                        new[]
                        {
                            (1L, 1L, ""), // 1-5位: 直接显示
                            (100000L, 1000L, "K"), // 6-7位: x.xxK
                            (10000000L, 1000000L, "M"), // 8-10位: x.xxM
                            (1000000000L, 1000000000L, "B"), // 11-13位: x.xxB
                            (100000000000L, 1000000000000L, "T") // 14位及以上: x.xxT
                        }
                    },
                    {
                        SchemeType.Power,
                        new[]
                        {
                            (1L, 1L, ""), // 1-7位: 直接显示
                            (1000000L, 1000000L, "M"), // 8-10位: x.xxM
                            (1000000000L, 1000000000L, "B"), // 11-13位: x.xxB
                            (1000000000000L, 1000000000000L, "T") // 14位及以上: x.xxT
                        }
                    }
                };

            public static string CompressNumber(long number, int decimalPlaces = 1)
            {
                return CompressNumber(number.ToString(), Schemes[SchemeType.Currency], decimalPlaces);
            }

            public static string CompressNumber(string number, int decimalPlaces = 1)
            {
                return CompressNumber(number, Schemes[SchemeType.Currency], decimalPlaces);
            }

            public static string CompressStandardNumber(long number, int decimalPlaces = 1)
            {
                return CompressNumber(number.ToString(), Schemes[SchemeType.Standard], decimalPlaces);
            }

            public static string CompressPowerNumber(long number)
            {
                return CompressNumber(number.ToString(), Schemes[SchemeType.Power]);
            }

            public static string CompressPowerNumber(string number)
            {
                return CompressNumber(number, Schemes[SchemeType.Power]);
            }

            private static string CompressNumber(string number, (long overValue, long divisor, string suffix)[] scheme,
                int decimalPlaces = 1)
            {
                if (!TryNormalizeIntegerString(number, out var normalizedNumber, out var isNegative))
                {
                    throw new ArgumentException("number must be a valid integer string", nameof(number));
                }

                if (normalizedNumber == "0")
                {
                    return "0";
                }

                var selected = scheme[0];
                foreach (var (overValue, divisor, suffix) in scheme)
                {
                    if (IsGreaterThanOrEqual(normalizedNumber, overValue))
                        selected = (overValue, divisor, suffix);
                    else
                        break;
                }

                var result = FormatScaledNumber(normalizedNumber, selected.divisor, decimalPlaces) + selected.suffix;

                return isNegative ? "-" + result : result;
            }

            private static bool TryNormalizeIntegerString(string number, out string normalizedNumber, out bool isNegative)
            {
                normalizedNumber = string.Empty;
                isNegative = false;

                if (string.IsNullOrWhiteSpace(number))
                {
                    return false;
                }

                var trimmedNumber = number.Trim();
                var startIndex = 0;

                if (trimmedNumber[0] == '-')
                {
                    isNegative = true;
                    startIndex = 1;
                }

                if (startIndex >= trimmedNumber.Length)
                {
                    return false;
                }

                while (startIndex < trimmedNumber.Length && trimmedNumber[startIndex] == '0')
                {
                    startIndex++;
                }

                if (startIndex == trimmedNumber.Length)
                {
                    normalizedNumber = "0";
                    isNegative = false;
                    return true;
                }

                for (var i = startIndex; i < trimmedNumber.Length; i++)
                {
                    if (!char.IsDigit(trimmedNumber[i]))
                    {
                        return false;
                    }
                }

                normalizedNumber = trimmedNumber.Substring(startIndex);
                return true;
            }

            private static bool IsGreaterThanOrEqual(string normalizedNumber, long value)
            {
                var valueText = value.ToString();
                if (normalizedNumber.Length != valueText.Length)
                {
                    return normalizedNumber.Length > valueText.Length;
                }

                return string.CompareOrdinal(normalizedNumber, valueText) >= 0;
            }

            private static string FormatScaledNumber(string normalizedNumber, long divisor, int decimalPlaces)
            {
                if (decimalPlaces < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(decimalPlaces));
                }

                var divisorDigits = divisor == 1 ? 0 : divisor.ToString().Length - 1;
                var integerLength = normalizedNumber.Length - divisorDigits;
                var integerPart = integerLength > 0 ? normalizedNumber.Substring(0, integerLength) : "0";
                var fractionalSource = integerLength > 0
                    ? normalizedNumber.Substring(integerLength)
                    : new string('0', -integerLength) + normalizedNumber;

                return RoundScaledNumber(integerPart, fractionalSource, decimalPlaces);
            }

            private static string RoundScaledNumber(string integerPart, string fractionalSource, int decimalPlaces)
            {
                var keptFraction = decimalPlaces > 0
                    ? fractionalSource.PadRight(decimalPlaces, '0').Substring(0, decimalPlaces)
                    : string.Empty;

                if (ShouldRoundUp(integerPart, keptFraction, fractionalSource, decimalPlaces))
                {
                    var combinedNumber = string.Concat(integerPart, keptFraction);
                    var roundedNumber = IncrementNumericString(combinedNumber);
                    var integerLength = roundedNumber.Length - decimalPlaces;
                    integerPart = integerLength > 0 ? roundedNumber.Substring(0, integerLength) : "0";
                    keptFraction = decimalPlaces > 0
                        ? roundedNumber.Substring(Math.Max(0, integerLength)).PadLeft(decimalPlaces, '0')
                        : string.Empty;
                }

                keptFraction = keptFraction.TrimEnd('0');
                return keptFraction.Length > 0 ? integerPart + "." + keptFraction : integerPart;
            }

            private static bool ShouldRoundUp(string integerPart, string keptFraction, string fractionalSource,
                int decimalPlaces)
            {
                if (fractionalSource.Length <= decimalPlaces)
                {
                    return false;
                }

                var roundDigit = fractionalSource[decimalPlaces] - '0';
                if (roundDigit > 5)
                {
                    return true;
                }

                if (roundDigit < 5)
                {
                    return false;
                }

                for (var i = decimalPlaces + 1; i < fractionalSource.Length; i++)
                {
                    if (fractionalSource[i] != '0')
                    {
                        return true;
                    }
                }

                var lastKeptDigit = decimalPlaces > 0
                    ? keptFraction[keptFraction.Length - 1] - '0'
                    : integerPart[integerPart.Length - 1] - '0';

                return lastKeptDigit % 2 != 0;
            }

            private static string IncrementNumericString(string number)
            {
                var chars = number.ToCharArray();

                for (var i = chars.Length - 1; i >= 0; i--)
                {
                    if (chars[i] == '9')
                    {
                        chars[i] = '0';
                        continue;
                    }

                    chars[i]++;
                    return new string(chars);
                }

                return "1" + new string(chars);
            }

            public static Quaternion GetTargetRotate(Transform self, Transform target)
            {
                var dir = target.position - self.position;
                var angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                return Quaternion.Euler(0, 0, angle);
            }

            /// <summary>
            /// 格式化浮点数，保留指定小数位，如果小数部分全为0则不显示小数部分
            /// </summary>
            /// <param name="value">要格式化的浮点数</param>
            /// <param name="decimalPlaces">要保留的小数位数</param>
            /// <returns>格式化后的字符串</returns>
            public static string FormatFloat(float value, int decimalPlaces = 2)
            {
                // 先转为decimal，提升精度
                var dValue = (decimal)value;
                var rounded = Math.Round(dValue, decimalPlaces, MidpointRounding.AwayFromZero);

                // 判断是否为整数
                if (decimal.Truncate(rounded) == rounded)
                {
                    return ((int)rounded).ToString();
                }

                // 格式化并去除多余0
                var format = "F" + decimalPlaces;
                var result = rounded.ToString(format);
                result = result.TrimEnd('0');
                if (result.EndsWith("."))
                {
                    result = result.TrimEnd('.');
                }

                return result;
            }

            /// <summary>
            /// 判断字符串是否为纯数值
            /// </summary>
            /// <param name="str">要判断的字符串</param>
            /// <param name="allowDecimal">是否允许小数</param>
            /// <param name="allowNegative">是否允许负数</param>
            /// <returns>是否为纯数值</returns>
            public static bool IsNumeric(string str, bool allowDecimal = true, bool allowNegative = true)
            {
                if (string.IsNullOrEmpty(str))
                    return false;

                var startIndex = 0;
                if (str[0] == '-')
                {
                    if (!allowNegative)
                        return false;
                    startIndex = 1;
                }

                var hasDigit = false;
                var hasDecimal = false;

                for (var i = startIndex; i < str.Length; i++)
                {
                    var c = str[i];
                    if (char.IsDigit(c))
                    {
                        hasDigit = true;
                        continue;
                    }

                    if (c == '.' && allowDecimal && !hasDecimal)
                    {
                        hasDecimal = true;
                        continue;
                    }

                    return false;
                }

                return hasDigit;
            }

            public static string GenerateMD5(string input)
            {
                using var md5 = MD5.Create();
                return BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(input))).Replace("-", "").ToLower();
            }

            /// <summary>
            /// 将一个数字各个位置的数拆到一个列表内
            /// </summary>
            /// <param name="number"></param>
            /// <returns></returns>
            public static List<int> SplitNumberIntoDigits(int number)
            {
                var digits = new List<int>();

                // 处理数字为0的特殊情况
                if (number == 0)
                {
                    digits.Add(0);
                    return digits;
                }

                // 将数字转换为正数处理
                number = Math.Abs(number);

                // 通过循环将每个位置上的数字加入到列表中
                while (number > 0)
                {
                    digits.Insert(0, number % 10); // 将最后一位插入到列表的开头
                    number /= 10; // 去掉最后一位
                }

                return digits;
            }


            public class PercentItem
            {
                public double Value { get; set; } // 修正后的百分比数值 (如 33.34)
                public string Text { get; set; } // 格式化字符串 (如 "33.34%")
            }

            public static List<PercentItem> GetPercentages(List<int> numbers)
            {
                if (numbers == null || numbers.Count == 0) return new List<PercentItem>();

                var total = numbers.Sum(x => (long)x);
                if (total == 0) return numbers.Select(n => new PercentItem { Value = 0, Text = "0.00%" }).ToList();

                var list = new List<(PercentItem item, double remainder)>();
                double sum = 0;

                foreach (var n in numbers)
                {
                    var exact = (double)n / total * 100.0;
                    var val = Math.Floor(exact * 100.0) / 100.0;
                    var rem = exact - val;

                    list.Add((new PercentItem { Value = val, Text = val.ToString("F2") + "%" }, rem));
                    sum += val;
                }

                var needed = (int)Math.Round((100.00 - sum) * 100.0 + 1e-9);

                if (needed <= 0) return list.Select(x => x.item).ToList();

                {
                    foreach (var entry in list.OrderByDescending(x => x.remainder).Take(needed))
                    {
                        entry.item.Value += 0.01;
                        entry.item.Text = entry.item.Value.ToString("F2") + "%";
                    }
                }

                return list.Select(x => x.item).ToList();
            }
        }
    }
}