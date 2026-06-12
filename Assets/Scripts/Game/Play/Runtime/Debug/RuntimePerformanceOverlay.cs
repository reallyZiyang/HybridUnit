using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Game.Play.Rendering.Runtime;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace Game.Play.Debugging
{
    public sealed class RuntimePerformanceOverlay : MonoBehaviour
    {
        private const float HostSearchIntervalSeconds = 1f;
        private const int FrameSampleCount = 120;

        private enum OverlayMode
        {
            Auto,
            GenericUnity,
            WeixinMiniGame
        }

        [SerializeField] private bool visible = true;
        [SerializeField] private bool dontDestroyOnLoad = true;
        [SerializeField] private KeyCode toggleKey = KeyCode.F8;
        [SerializeField] private OverlayMode mode = OverlayMode.Auto;
        [SerializeField, Min(0.05f)] private float refreshInterval = 0.25f;
        [SerializeField] private Rect windowRect = new(12f, 12f, 360f, 260f);
        [SerializeField, Min(8)] private int fontSize = 20;
        [SerializeField] private bool showMemory = true;
        [SerializeField] private bool showDrawMesh = true;

        private readonly StringBuilder builder = new(1024);
        private readonly float[] frameMsSamples = new float[FrameSampleCount];
        private readonly Dictionary<string, PerfValueTracker> trackers = new(64);

        private ProfilerRecorder gcAllocatedRecorder;
        private ProfilerRecorder setPassRecorder;
        private ProfilerRecorder drawCallsRecorder;
        private ProfilerRecorder verticesRecorder;
        private BattleDrawMeshInstanceRenderHost drawMeshHost;
        private GUIStyle labelStyle;
        private GUIStyle windowStyle;
        private string cachedText = string.Empty;
        private string cachedTitle = "Perf";
        private float nextRefreshTime;
        private float nextHostSearchTime;
        private float smoothedDeltaTime;
        private float maxFrameMs;
        private int frameSampleCursor;
        private bool gcRecorderAvailable;
        private bool setPassRecorderAvailable;
        private bool drawCallsRecorderAvailable;
        private bool verticesRecorderAvailable;

        private void Awake()
        {
            if (dontDestroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnEnable()
        {
            TryStartRecorders();
        }

        private void OnDisable()
        {
            DisposeRecorders();
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                visible = !visible;
            }

            float deltaTime = Mathf.Max(0f, Time.unscaledDeltaTime);
            smoothedDeltaTime = smoothedDeltaTime <= 0f
                ? deltaTime
                : Mathf.Lerp(smoothedDeltaTime, deltaTime, 0.1f);
            TrackFrameSample(deltaTime * 1000f);

            if (Time.unscaledTime >= nextHostSearchTime)
            {
                nextHostSearchTime = Time.unscaledTime + HostSearchIntervalSeconds;
                RefreshDrawMeshHost();
            }

            if (Time.unscaledTime >= nextRefreshTime)
            {
                nextRefreshTime = Time.unscaledTime + refreshInterval;
                RebuildText();
            }
        }

        private void OnGUI()
        {
            if (!visible)
            {
                return;
            }

            EnsureStyles();
            windowRect = GUI.Window(GetInstanceID(), windowRect, DrawWindow, cachedTitle, windowStyle);
        }

        private void DrawWindow(int id)
        {
            GUILayout.Label(cachedText, labelStyle);
            GUI.DragWindow(new Rect(0f, 0f, windowRect.width, 24f));
        }

        private void TrackFrameSample(float frameMs)
        {
            frameMsSamples[frameSampleCursor] = frameMs;
            frameSampleCursor = (frameSampleCursor + 1) % frameMsSamples.Length;
            maxFrameMs = 0f;
            for (int i = 0; i < frameMsSamples.Length; i++)
            {
                if (frameMsSamples[i] > maxFrameMs)
                {
                    maxFrameMs = frameMsSamples[i];
                }
            }
        }

        private void RebuildText()
        {
            if (GetEffectiveMode() == OverlayMode.WeixinMiniGame)
            {
                BuildWeixinMiniGameText();
            }
            else
            {
                BuildGenericText();
            }

            cachedText = builder.ToString();
        }

        private void BuildGenericText()
        {
            builder.Length = 0;
            cachedTitle = "Perf - GenericUnity";

            float fps = smoothedDeltaTime > 0.00001f ? 1f / smoothedDeltaTime : 0f;
            float avgMs = smoothedDeltaTime * 1000f;
            float currentMs = Time.unscaledDeltaTime * 1000f;
            builder.Append("FPS: ").Append(fps.ToString("0.0")).Append("  Avg: ").Append(avgMs.ToString("0.00")).Append(" ms\n");
            builder.Append("Frame: ").Append(currentMs.ToString("0.00")).Append(" ms  Max: ").Append(maxFrameMs.ToString("0.00")).Append(" ms\n");
            builder.Append("TimeScale: ").Append(Time.timeScale.ToString("0.00"))
                .Append("  TargetFPS: ").Append(Application.targetFrameRate)
                .Append("  VSync: ").Append(QualitySettings.vSyncCount)
                .Append('\n');

            if (showMemory)
            {
                builder.Append('\n');
                builder.Append("Managed: ").Append(FormatBytes(GC.GetTotalMemory(false))).Append('\n');
                builder.Append("Allocated: ").Append(FormatBytes(Profiler.GetTotalAllocatedMemoryLong())).Append('\n');
                builder.Append("Reserved: ").Append(FormatBytes(Profiler.GetTotalReservedMemoryLong())).Append('\n');
                builder.Append("GC/frame: ").Append(FormatGcAllocated()).Append('\n');
            }

            if (showDrawMesh)
            {
                builder.Append('\n');
                AppendDrawMeshStats(false);
            }
        }

        private void BuildWeixinMiniGameText()
        {
            builder.Length = 0;
            cachedTitle = "Perf - WeixinMiniGame";

            float fps = smoothedDeltaTime > 0.00001f ? 1f / smoothedDeltaTime : 0f;
            float currentMs = WeixinMiniGameStats.TryGetFloat("GetEXFrameTime", out float wxFrameMs)
                ? wxFrameMs
                : Time.unscaledDeltaTime * 1000f;

            builder.AppendLine("-------------Frame-------------");
            AppendTracked("FPS", fps, "0.0");
            AppendTracked("FrameTime(ms)", currentMs, "0.00");
            AppendTracked("MaxFrame(ms)", maxFrameMs, "0.00");

            if (showMemory)
            {
                builder.AppendLine("-------------WASM--------------");
                AppendWeixinBytesAsMb("WASM TotalHeap", "GetTotalMemorySize");
                AppendWeixinBytesAsMb("WASM Dynamic", "GetDynamicMemorySize");
                AppendWeixinBytesAsMb("WASM UsedHeap", "GetUsedMemorySize");
                AppendWeixinBytesAsMb("WASM UnAllocated", "GetUnAllocatedMemorySize");

                builder.AppendLine("-------------Unity Memory------");
                AppendTracked("Mono Used(MB)", BytesToMb(Profiler.GetMonoUsedSizeLong()), "0.00");
                AppendTracked("Mono Reserved(MB)", BytesToMb(Profiler.GetMonoHeapSizeLong()), "0.00");
                AppendTracked("Native Alloc(MB)", BytesToMb(Profiler.GetTotalAllocatedMemoryLong()), "0.00");
                AppendTracked("Native Reserved(MB)", BytesToMb(Profiler.GetTotalReservedMemoryLong()), "0.00");
                AppendTracked("Native Unused(MB)", BytesToMb(Profiler.GetTotalUnusedReservedMemoryLong()), "0.00");

                string gcFrame = FormatGcAllocated();
                if (!string.Equals(gcFrame, "N/A", StringComparison.Ordinal))
                {
                    builder.Append("GC/frame: ").Append(gcFrame).Append('\n');
                }

                builder.AppendLine("-------------AssetBundle-------");
                AppendWeixinNumber("AB Memory Count", "GetBundleNumberInMemory");
                AppendWeixinBytesAsMb("AB Memory Size", "GetBundleSizeInMemory");
                AppendWeixinNumber("AB Disk Count", "GetBundleNumberOnDisk");
                AppendWeixinBytesAsMb("AB Disk Size", "GetBundleSizeOnDisk");
            }

            builder.AppendLine("-------------Render------------");
            AppendRecorder("SetPass", setPassRecorder, setPassRecorderAvailable);
            AppendRecorder("DrawCalls", drawCallsRecorder, drawCallsRecorderAvailable);
            AppendRecorder("Vertices", verticesRecorder, verticesRecorderAvailable);

            if (showDrawMesh)
            {
                builder.AppendLine("-------------DrawMesh----------");
                AppendDrawMeshStats(true);
            }
        }

        private OverlayMode GetEffectiveMode()
        {
            if (mode != OverlayMode.Auto)
            {
                return mode;
            }

#if WEIXINMINIGAME && !UNITY_EDITOR
            return OverlayMode.WeixinMiniGame;
#else
            return OverlayMode.GenericUnity;
#endif
        }

        private void AppendDrawMeshStats(bool tracked)
        {
            if (drawMeshHost != null)
            {
                if (tracked)
                {
                    AppendTracked("DrawMesh Active", drawMeshHost.ActiveCount, "0");
                    AppendTracked("DrawMesh Drawn", drawMeshHost.LastDrawInstanceCount, "0");
                }
                else
                {
                    builder.Append("DrawMesh Active: ").Append(drawMeshHost.ActiveCount).Append('\n');
                    builder.Append("DrawMesh Drawn: ").Append(drawMeshHost.LastDrawInstanceCount).Append('\n');
                }

                builder.Append("Draw Camera: ")
                    .Append(string.IsNullOrEmpty(drawMeshHost.LastDrawCameraName) ? "N/A" : drawMeshHost.LastDrawCameraName)
                    .Append('\n');
            }
            else
            {
                builder.Append("DrawMesh: host not found\n");
            }
        }

        private void AppendRecorder(string label, ProfilerRecorder recorder, bool available)
        {
            if (!available || !recorder.Valid)
            {
                builder.Append(label).Append(": N/A\n");
                return;
            }

            AppendTracked(label, recorder.LastValue, "0");
        }

        private void AppendWeixinNumber(string label, string methodName)
        {
            if (WeixinMiniGameStats.TryGetFloat(methodName, out float value))
            {
                AppendTracked(label, value, "0");
            }
            else
            {
                builder.Append(label).Append(": N/A\n");
            }
        }

        private void AppendWeixinBytesAsMb(string label, string methodName)
        {
            if (WeixinMiniGameStats.TryGetLong(methodName, out long bytes))
            {
                AppendTracked(label + "(MB)", BytesToMb(bytes), "0.00");
            }
            else
            {
                builder.Append(label).Append(": N/A\n");
            }
        }

        private void AppendTracked(string label, float value, string format)
        {
            if (!trackers.TryGetValue(label, out PerfValueTracker tracker))
            {
                tracker = new PerfValueTracker();
                trackers.Add(label, tracker);
            }

            tracker.Update(value);
            builder.Append(label).Append(": [")
                .Append(tracker.Current.ToString(format)).Append(" / ")
                .Append(tracker.Min.ToString(format)).Append(" / ")
                .Append(tracker.Max.ToString(format)).Append("]\n");
        }

        private string FormatGcAllocated()
        {
            if (!gcRecorderAvailable || !gcAllocatedRecorder.Valid)
            {
                return "N/A";
            }

            return FormatBytes(gcAllocatedRecorder.LastValue);
        }

        private static string FormatBytes(long bytes)
        {
            const float kb = 1024f;
            const float mb = kb * 1024f;
            if (bytes >= mb)
            {
                return (bytes / mb).ToString("0.00") + " MB";
            }

            if (bytes >= kb)
            {
                return (bytes / kb).ToString("0.00") + " KB";
            }

            return bytes + " B";
        }

        private static float BytesToMb(long bytes)
        {
            return bytes / (1024f * 1024f);
        }

        private void TryStartRecorders()
        {
            DisposeRecorders();
            gcRecorderAvailable = TryStartRecorder(ProfilerCategory.Memory, "GC Allocated In Frame", out gcAllocatedRecorder);
            setPassRecorderAvailable = TryStartRecorder(ProfilerCategory.Render, "SetPass Calls Count", out setPassRecorder);
            drawCallsRecorderAvailable = TryStartRecorder(ProfilerCategory.Render, "Draw Calls Count", out drawCallsRecorder);
            verticesRecorderAvailable = TryStartRecorder(ProfilerCategory.Render, "Vertices Count", out verticesRecorder);
        }

        private static bool TryStartRecorder(ProfilerCategory category, string statName, out ProfilerRecorder recorder)
        {
            try
            {
                recorder = ProfilerRecorder.StartNew(category, statName);
                return recorder.Valid;
            }
            catch
            {
                recorder = default;
                return false;
            }
        }

        private void DisposeRecorders()
        {
            DisposeRecorder(ref gcAllocatedRecorder);
            DisposeRecorder(ref setPassRecorder);
            DisposeRecorder(ref drawCallsRecorder);
            DisposeRecorder(ref verticesRecorder);

            gcRecorderAvailable = false;
            setPassRecorderAvailable = false;
            drawCallsRecorderAvailable = false;
            verticesRecorderAvailable = false;
        }

        private static void DisposeRecorder(ref ProfilerRecorder recorder)
        {
            if (recorder.Valid)
            {
                recorder.Dispose();
            }

            recorder = default;
        }

        private void RefreshDrawMeshHost()
        {
#if UNITY_2023_1_OR_NEWER
            drawMeshHost = FindFirstObjectByType<BattleDrawMeshInstanceRenderHost>();
#else
            drawMeshHost = FindObjectOfType<BattleDrawMeshInstanceRenderHost>();
#endif
        }

        private void EnsureStyles()
        {
            if (labelStyle != null && labelStyle.fontSize == fontSize)
            {
                return;
            }

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                normal = { textColor = Color.white },
                wordWrap = false
            };
            windowStyle = new GUIStyle(GUI.skin.window)
            {
                fontSize = fontSize
            };
        }

        private sealed class PerfValueTracker
        {
            public float Current { get; private set; }
            public float Min { get; private set; } = float.PositiveInfinity;
            public float Max { get; private set; } = float.NegativeInfinity;

            public void Update(float value)
            {
                Current = value;
                if (value < Min)
                {
                    Min = value;
                }

                if (value > Max)
                {
                    Max = value;
                }
            }
        }

        private static class WeixinMiniGameStats
        {
#if WEIXINMINIGAME || UNITY_WEBGL
            private static readonly string[] TypeNames =
            {
                "WeChatWASM.WXSDKManagerHandler, WxWasmSDKRuntime",
                "WeChatWASM.WXSDKManagerHandler, Assembly-CSharp",
                "WeChatWASM.WXSDKManagerHandler"
            };

            private static Type cachedType;
            private static PropertyInfo instanceProperty;
            private static readonly Dictionary<string, MethodInfo> Methods = new(16);
            private static bool initialized;

            public static bool TryGetLong(string methodName, out long value)
            {
                value = 0;
                if (!TryInvoke(methodName, out object result) || result == null)
                {
                    return false;
                }

                try
                {
                    value = Convert.ToInt64(result);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            public static bool TryGetFloat(string methodName, out float value)
            {
                value = 0f;
                if (!TryInvoke(methodName, out object result) || result == null)
                {
                    return false;
                }

                try
                {
                    value = Convert.ToSingle(result);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            private static bool TryInvoke(string methodName, out object result)
            {
                result = null;
                if (!EnsureInitialized() || instanceProperty == null)
                {
                    return false;
                }

                object instance;
                try
                {
                    instance = instanceProperty.GetValue(null);
                }
                catch
                {
                    return false;
                }

                if (instance == null)
                {
                    return false;
                }

                if (!Methods.TryGetValue(methodName, out MethodInfo method))
                {
                    method = cachedType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
                    Methods.Add(methodName, method);
                }

                if (method == null)
                {
                    return false;
                }

                try
                {
                    result = method.Invoke(instance, null);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            private static bool EnsureInitialized()
            {
                if (initialized)
                {
                    return cachedType != null;
                }

                initialized = true;
                for (int i = 0; i < TypeNames.Length; i++)
                {
                    cachedType = Type.GetType(TypeNames[i]);
                    if (cachedType != null)
                    {
                        break;
                    }
                }

                instanceProperty = cachedType?.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
                return cachedType != null;
            }
#else
            public static bool TryGetLong(string methodName, out long value)
            {
                value = 0;
                return false;
            }

            public static bool TryGetFloat(string methodName, out float value)
            {
                value = 0f;
                return false;
            }
#endif
        }
    }
}
