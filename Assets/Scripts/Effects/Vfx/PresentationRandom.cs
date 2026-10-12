using UnityEngine;
namespace Effects.Vfx
{
    /// <summary>장식 파티클/시트 순서는 치명타·대상 추첨의 Unity 난수열을 소비하지 않는다.</summary>
    internal static class PresentationRandom
    {
        private static readonly System.Random Source = new(81723);
        public static float value => (float)Source.NextDouble();
        public static float Range(float min,float max) => Mathf.Lerp(min,max,value);
        public static int Range(int min,int max) => max<=min ? min : Source.Next(min,max);
        public static Vector2 insideUnitCircle
        {
            get { float a=value*Mathf.PI*2, r=Mathf.Sqrt(value); return new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r; }
        }
    }
}
