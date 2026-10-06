using System;
using System.Collections.Generic;

/// <summary>96_vfx.yaml의 손그림 부품 한 개.</summary>
[Serializable]
public sealed class VfxPartData
{
    public string id;
    public string category;
    public string inkColor;
    public float inkAlpha;
    public string darkBodyColor;
    public bool darkDesaturate;
    public float desaturate;
    public bool calmLight;
    public bool useHitAlpha;
    public bool edgeUnderlay;
    public float affectedDelay;
    public int startFrame;
    public int tailHoldFrame = -1;
    public float tailHoldSeconds;
    public string renderer = "flipbook";
    public string sheet;
    public int columns = 1;
    public int rows = 1;
    public float fps = 30f;
    public string bodyColor = "#FFFFFFFF";
    public string edgeColor = "#FFFFFFFF";
    public string coreColor = "#FFFFFFFF";
    public float scaleX = 1f;
    public float scaleY = 1f;
    public int peakFrame = -1;
    public string accentPartId;
    public float peakAlpha = 0.86f;
    public float glowAlpha = 0.62f;
    /// <summary>짧은 연발 부품이 정점에서 읽히는 시간을 보장하는 추가 프레임 유지 시간(게임 시간).</summary>
    public float peakHoldSeconds;
    public float focusStrength;
    public float focusFadeIn = 0.08f;
    public float focusHold = 0.24f;
    public float focusFadeOut = 0.24f;
    public float flashStrength;
    public float shake;
    public float sourceAngle = 45f;
    public float lengthInCards = 1.70f;
    public float thicknessInCards = 0.13f;
    public float drawDuration = 0.18f;
    public float centerCrossFraction = 0.50f;
    public float lineDelay = 0.022f;
    public float lineSpacingInCards = 0.16f;
    public float lingerDuration = 0.10f;
    public float fadeDuration = 0.17f;
    public float revealSoftness = 0.040f;
    public float tipWidth = 0.070f;
    public float cardKickStrength = 0.55f;
    public string secondaryPartId;
    /// <summary>방향성 피격 원화 안에서 실제 충돌 끝점의 정규화 좌표. 기본은 그림 중심.</summary>
    public float impactAnchorX = 0.5f;
    public float impactAnchorY = 0.5f;
}

/// <summary>스킬의 prepare/cast/hit/affected/screen 슬롯 한 겹.</summary>
[Serializable]
public sealed class VfxSlotData
{
    public string slot;
    public bool portraitAnchor;
    public bool steadyBody;
    public bool recolor;
    public bool supportInk;
    public float supportScale = 1f;
    public bool coinBurst;
    public string partId;
    public string bodyColor;
    public string edgeColor;
    public string coreColor;
    public float scale = 1f;
    public float scaleX;
    public float scaleY;
    public float offsetX;
    public float offsetY;
    public float forwardOffset;
    public string direction = "casterToTarget";
    public bool sourceFacesRight = true;
    public float angleOffset;
    public float timeOffset;
    public int peakFrame = -1;
    /// <summary>시각 재생 속도와 분리해 기존 판정 시각을 보존할 때 쓰는 초 단위 지연. 음수면 peakFrame을 따른다.</summary>
    public float impactDelay = -1f;
    public bool triggersImpact;
    public int orderOffset;
    public bool useCasterColor;
}

/// <summary>코드/SFX 논리 키 하나에 대응하는 데이터 기반 연출 정의.</summary>
[Serializable]
public sealed class VfxSkillData
{
    public string key;
    public string multiTargetMode = "simultaneous";
    public List<VfxSlotData> slots = new();
}

/// <summary>상태 키가 별도 affected 부품을 지정할 때 쓰는 선택적 덮어쓰기.</summary>
[Serializable]
public sealed class VfxStatusBindingData
{
    public string statusKey;
    public string partId;
}

/// <summary>내부 VFX 분류가 기본 시전에 사용할 표준 부품.</summary>
[Serializable]
public sealed class VfxClassCastBindingData
{
    public string vfxClass;
    public string partId;
}

[Serializable]
public sealed class VfxDataList
{
    public List<VfxPartData> parts = new();
    public List<VfxSkillData> skills = new();
    public List<VfxStatusBindingData> statusBindings = new();
    public List<VfxClassCastBindingData> classCastBindings = new();
}
