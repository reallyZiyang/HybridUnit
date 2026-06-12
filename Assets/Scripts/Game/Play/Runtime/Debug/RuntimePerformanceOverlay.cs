using System;
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

        [SerializeField] private bool visible = true;
        [SerializeField] private bool dontDestroyOnLoad = true;
        [SerializeField] private KeyCode toggleKey = KeyCode.F8;
        [SerializeField, Min(0.05f)] private float refreshInterval = 0.25f;
        [SerializeField] private Rect windowRect = new(12f, 12f, 360f, 260f);
        [SerializeField, Min(8)] private int fontSize = 20;
        [SerializeField] private bool showMemory = true;
        [SerializeField] private bool showDrawMesh = true;

        private readonly StringBuilder builder = new(1024);
        private readonly float[] frameMsSamples = new float[FrameSampleCount];

        private ProfilerRecorder gcAllocatedRecorder;
        private BattleDrawMeshInstanceRenderHost drawMeshHost;
        private GUIStyle labelStyle;
        private GUIStyle windowStyle;
        private string cachedText = string.Empty;
        private float nextRefreshTime;
        private float nextHostSearchTime;
        private float smoothedDeltaTime;
        private float maxFrameMs;
        private int frameSampleCursor;
        private bool gcRecorderAvailable;

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
            windowRect = GUI.Window(GetInstanceID(), windowRect, DrawWindow, "Runtime Performance", windowStyle);
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
            builder.Length = 0;

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
                if (drawMeshHost != null)
                {
                    builder.Append("DrawMesh Active: ").Append(drawMeshHost.ActiveCount).Append('\n');
                    builder.Append("DrawMesh Drawn: ").Append(drawMeshHost.LastDrawInstanceCount).Append('\n');
                    builder.Append("Draw Camera: ").Append(string.IsNullOrEmpty(drawMeshHost.LastDrawCameraName) ? "N/A" : drawMeshHost.LastDrawCameraName).Append('\n');
                }
                else
                {
                    builder.Append("DrawMesh: host not found\n");
                }
            }

            cachedText = builder.ToString();
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

        private void TryStartRecorders()
        {
            DisposeRecorders();
            try
            {
                gcAllocatedRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
                gcRecorderAvailable = gcAllocatedRecorder.Valid;
            }
            catch
            {
                gcRecorderAvailable = false;
            }
        }

        private void DisposeRecorders()
        {
            if (gcAllocatedRecorder.Valid)
            {
                gcAllocatedRecorder.Dispose();
            }

            gcRecorderAvailable = false;
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
    }
}
