#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;

namespace Core
{
    /// <summary>에디터 감시기와 Play 모드 실행기가 공유하는 SessionState 키.</summary>
    public static class VerifyHarnessSession
    {
        public const string StateKey = "NTL.Verify.State";
        public const string RequestJsonKey = "NTL.Verify.RequestJson";
        public const string RequestDirectoryKey = "NTL.Verify.RequestDirectory";
        public const string OriginalPlaySceneKey = "NTL.Verify.OriginalPlayScene";
        public const string OriginalEditorSceneSetupKey = "NTL.Verify.OriginalEditorSceneSetup";
        public const string ConsoleJsonKey = "NTL.Verify.ConsoleJson";
        public const string EditorWarningsJsonKey = "NTL.Verify.EditorWarningsJson";
        public const string RuntimeResultFile = ".runtime-result.json";
    }

    [Serializable]
    public sealed class VerifyRequest
    {
        public List<VerifyScenarioRequest> scenarios = new();
        public string[] scenarioNames = Array.Empty<string>();
        public int partyLevel;
        public float captureSeconds = 0.9f;
        public int everyFrames = 3;
        public float timeoutSeconds = 30f;
        public int captureWidth = 1920;
        public int captureHeight = 1080;
        public string lineup = "party5";
        public string backgroundOverridePath;
        public string vfxVariant;
        public string vfxStyle;
    }

    [Serializable]
    public sealed class VerifyScenarioRequest
    {
        public string name;
        public int stage;
        public int partyLevel;
        public float captureSeconds;
        public int everyFrames;
        public float warmupSeconds;
        public float timeScale;
        public string trigger;
        public string lineup;
        public string backgroundOverridePath;
        public string vfxVariant;
        public string vfxStyle;
        public string[] hiddenCardLayers = Array.Empty<string>();
        public bool dumpPortraitCopies;

        // -1은 시나리오 기본값, 0은 끔, 1은 켬이다. JsonUtility에서 nullable 대신 쓴다.
        public int allyInvincible = -1;
        public int enemyInvincible = -1;
    }

    [Serializable]
    public sealed class VerifyCaptureRecord
    {
        public string scenario;
        public string tag;
        public string path;
        public int frame;
        public float realtime;
        public float backgroundLuminance;
    }

    [Serializable]
    public sealed class VerifyScenarioResult
    {
        public string name;
        public bool success;
        public string message;
        public int captureCount;
        public float timeScale;
        public float startedRealtime;
        public float finishedRealtime;
    }

    [Serializable]
    public sealed class VerifyLogRecord
    {
        public string type;
        public string message;
        public string stackTrace;
    }

    [Serializable]
    public sealed class VerifyLogCollection
    {
        public List<VerifyLogRecord> entries = new();
    }

    [Serializable]
    public sealed class VerifySfxRecord
    {
        public string scenario;
        public float realtime;
        public string cue;
        public string clip;
        public string mixerGroup;
        public bool played;
        public string reason;
        public float volume;
        public float pitch;
        public float timeScale;
    }

    [Serializable]
    public sealed class VerifyVisualEventRecord
    {
        public string scenario;
        public float realtime;
        public string tag;
        public int frame;
    }

    [Serializable]
    public sealed class VerifyVfxSlotRecord
    {
        public string scenario;
        public float realtime;
        public int frame;
        public string skillKey;
        public string slot;
        public string partId;
        public string phase;
        public int slotIndex;
        public string caster;
        public string target;
        public float screenX;
        public float screenY;
        public float worldX;
        public float worldY;
        public float angle;
        public bool mirrored;
        public string sortingLayer;
        public int sortingOrder;
    }

    [Serializable]
    public sealed class VerifyPortraitRectRecord
    {
        public string scenario;
        public float realtime;
        public int frame;
        public string unit;
        public float xMin;
        public float yMin;
        public float xMax;
        public float yMax;
    }

    [Serializable]
    public sealed class VerifyUiRectRecord
    {
        public string scenario;
        public float realtime;
        public int frame;
        public int groupId;
        public string element;
        public string text;
        public float xMin;
        public float yMin;
        public float xMax;
        public float yMax;
    }

    [Serializable]
    public sealed class VerifyStringCollection
    {
        public List<string> entries = new();
    }

    [Serializable]
    public sealed class VerifyMechanicRecord
    {
        public string scenario;
        public string check;
        public string expected;
        public string actual;
        public bool passed;
    }

    [Serializable]
    public sealed class VerifyRunResult
    {
        public bool success;
        public string startedAtUtc;
        public string finishedAtUtc;
        public string resolution;
        public string message;
        public List<VerifyScenarioResult> scenarios = new();
        public List<VerifyCaptureRecord> captures = new();
        public List<VerifyVisualEventRecord> visualEvents = new();
        public List<VerifyVfxSlotRecord> vfxSlots = new();
        public List<VerifyPortraitRectRecord> portraitRects = new();
        public List<VerifyUiRectRecord> uiRects = new();
        public List<VerifySfxRecord> sfxEvents = new();
        public List<VerifyMechanicRecord> mechanics = new();
        public List<VerifyLogRecord> consoleExceptions = new();
        public List<string> warnings = new();
    }

    /// <summary>
    /// 짧은 연출의 정확한 프레임을 좌표 클릭 없이 잡기 위한 개발 전용 이벤트 버스.
    /// 발행부는 판정이나 VFX 흐름을 바꾸지 않고 태그만 알린다.
    /// </summary>
    public static class VerifyEvents
    {
        public static event Action<string> Raised;

        public static void Raise(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return;
            Raised?.Invoke(tag.Trim().ToLowerInvariant());
        }
    }
}
#endif
