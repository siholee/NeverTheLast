using System;
using System.Collections.Generic;

namespace Managers
{
    /// <summary>97_stage_figures.yaml. 좌상단 원점, 스탠딩 높이 기준 비율이다.</summary>
    [Serializable]
    public sealed class StageFigureData
    {
        public string sprite;
        public float top = float.NaN, chin = float.NaN, cx = float.NaN, feet = float.NaN;
        public string facing;
        public bool deform = true;
        public CutInFaceData cutinFace;
        public SdFrameData sd;
        public SummonStageData summon;

        public void Validate()
        {
            if (!(top >= 0f && chin > top && feet > chin + .012f && feet <= 1f && cx > 0f && cx < 1f) ||
                (facing != "left" && facing != "right"))
                throw new InvalidOperationException($"SD 보정 필드 누락/범위 오류: {sprite ?? "default"}");
            cutinFace?.Validate();
            sd?.Validate();
            summon?.Validate();
        }
    }

    /// <summary>주인 발 기준 화면 픽셀. x는 적 방향, y는 화면 아래, 깊이 배율은 표시 시 적용한다.</summary>
    [Serializable]
    public sealed class SummonStageData
    {
        public float size, hover, back, up;
        public bool aboveUnits;
        public string badge;
        public List<SdAnchorData> slots;
        public void Validate()
        {
            if (!(size > 0 && size <= 1 && hover >= 0) ||
                (slots != null && (slots.Count != 3 || slots.Exists(p => p == null || float.IsNaN(p.x) || float.IsNaN(p.y)))))
                throw new InvalidOperationException("소환수 Stage 크기/둘레 슬롯 오류");
        }
    }

    /// <summary>컷인 전용 얼굴 중심. 스탠딩 좌상단 원점의 x/y 비율이다.</summary>
    [Serializable]
    public sealed class CutInFaceData
    {
        public float x = float.NaN, y = float.NaN;
        public void Validate()
        {
            if (!(x >= 0f && x <= 1f && y >= 0f && y <= 1f))
                throw new InvalidOperationException("컷인 얼굴점 비율 누락/범위 오류");
        }
    }

    /// <summary>C3 원본 1024×1280, 좌상단 원점의 픽셀 좌표. 모든 포즈가 같은 보정을 쓴다.</summary>
    [Serializable]
    public sealed class SdFrameData
    {
        public string prefix;
        // 소환수 SD가 승인·등록되기 전 사용하는 비변형 원화. 5프레임이 등록되면 sd 블록만 교체한다.
        public string fallbackSprite;
        public float feetY = float.NaN, faceX = float.NaN, topY = float.NaN;
        public SdAnchorData head, chest, hand;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(prefix) || !(topY >= 0 && feetY > topY && feetY <= 1280 && faceX > 0 && faceX < 1024) ||
                head == null || chest == null || hand == null)
                throw new InvalidOperationException("SD 프레임 접두어/발/얼굴/앵커 누락");
            head.Validate(); chest.Validate(); hand.Validate();
        }
    }

    [Serializable]
    public sealed class SdAnchorData
    {
        public float x = float.NaN, y = float.NaN;
        public void Validate()
        {
            if (!(x >= 0 && x <= 1024 && y >= 0 && y <= 1280))
                throw new InvalidOperationException("SD 앵커 픽셀 범위 오류");
        }
    }

    [Serializable]
    public sealed class StageFigureDataList
    {
        public List<StageFigureData> figures;
        public StageFigureData @default;
        public SummonStageData swarm;

        public void Validate()
        {
            if (figures == null || @default == null) throw new InvalidOperationException("SD figures/default 누락");
            @default.Validate();
            if (swarm?.slots == null) throw new InvalidOperationException("기본 다수형 Stage 배치 누락");
            swarm.Validate();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (StageFigureData figure in figures)
            {
                if (figure == null || string.IsNullOrWhiteSpace(figure.sprite) || !names.Add(figure.sprite))
                    throw new InvalidOperationException("SD sprite 누락/중복");
                figure.Validate();
            }
        }
    }
}
