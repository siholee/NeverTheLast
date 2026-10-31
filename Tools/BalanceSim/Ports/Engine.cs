// 콘솔 호스트의 값 형식·수명 어댑터. Unity 바이너리를 로드하지 않는다.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
namespace UnityEngine
{
    public class SerializeField : Attribute { }
    public class HeaderAttribute : Attribute { public HeaderAttribute(string value) { } }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string value) { } }
    public class RangeAttribute : Attribute { public RangeAttribute(float min, float max) { } }
    public class Sprite { }
    public class Object
    {
        private static int Sequence;
        private readonly int InstanceId = ++Sequence;
        public int GetInstanceID() => InstanceId;
        public int GetEntityId() => InstanceId;
        public static void Destroy(object value) { }
        public static implicit operator bool(Object value) => value != null;
    }
    public class MonoBehaviour : Object
    {
        public GameObject gameObject => new GameObject(this);
        public Coroutine StartCoroutine(IEnumerator routine) => CoroutineLoop.Start(this, routine);
        public void StopCoroutine(Coroutine routine) => CoroutineLoop.Stop(routine);
        public void StopAllCoroutines() => CoroutineLoop.StopAll(this);
        public T GetComponent<T>() where T : class => this as T;
        public Transform transform = new();
    }
    public class GameObject
    {
        private object component;
        public GameObject(object value=null) {component=value;}
        public T GetComponent<T>() where T:class => component as T;
        public T AddComponent<T>() where T:new() => new T();
        public static T FindAnyObjectByType<T>() where T:class => Managers.GridManager.Instance as T;
    }
    public class Transform { public Vector3 position; public Vector3 localScale = new(1,1,1); }
    public class Coroutine
    {
        internal object Owner;
        internal Stack<IEnumerator> Stack = new();
        internal bool Done;
        internal float Wait;
        internal Coroutine Child;
        internal bool Running;
    }
    public static class CoroutineLoop
    {
        private static readonly List<Coroutine> Active = new();
        public static int Count => Active.Count;
        public static Coroutine Start(object owner, IEnumerator routine)
        {
            var job = new Coroutine { Owner = owner };
            job.Stack.Push(routine); Active.Add(job); Step(job); return job;
        }
        public static void Stop(Coroutine job) { if (job == null) return; job.Done = true; if(!job.Running) Dispose(job); }
        private static void Dispose(Coroutine job) { while(job.Stack.Count>0) (job.Stack.Pop() as IDisposable)?.Dispose(); }
        public static void StopAll(object owner) { foreach (var job in Active.ToArray()) if (job.Owner == owner) Stop(job); }
        public static void Clear() { foreach (var job in Active.ToArray()) Stop(job); Active.Clear(); }
        public static void Tick(float delta)
        {
            Time.deltaTime = delta; Time.time += delta;
            foreach (var job in Active.ToArray())
            {
                if (job.Done) continue;
                if (job.Child != null && !job.Child.Done) continue;
                job.Child = null;
                job.Wait -= delta; if (job.Wait > 0) continue;
                Step(job);
            }
            Active.RemoveAll(job => job.Done);
        }
        private static void Step(Coroutine job)
        {
            job.Running=true;
            try
            {
            int steps = 0;
            while (!job.Done && job.Stack.Count > 0)
            {
                if (++steps > 100000) throw new InvalidOperationException("코루틴 진행 상한 초과");
                var top = job.Stack.Peek();
                if (!top.MoveNext()) { (top as IDisposable)?.Dispose(); job.Stack.Pop(); continue; }
                if (top.Current is IEnumerator nested) { job.Stack.Push(nested); continue; }
                if (top.Current is Coroutine child) { job.Child = child; if (child.Done) continue; return; }
                if (top.Current is WaitForSeconds wait) job.Wait = wait.Seconds;
                return;
            }
            if (job.Stack.Count == 0) job.Done = true;
            }
            finally { job.Running=false;if(job.Done) Dispose(job); }
        }
    }
    public class WaitForSeconds { public float Seconds; public WaitForSeconds(float value) { Seconds = value; } }
    public class WaitForSecondsRealtime : WaitForSeconds { public WaitForSecondsRealtime(float value) : base(value) { } }
    public static class Time { public static float deltaTime = .02f, time; public static float unscaledDeltaTime => deltaTime; public static float unscaledTime => time; }
    public static class Debug
    {
        public static void Log(object value) { }
        public static void LogWarning(object value) => throw new InvalidOperationException(value?.ToString());
        public static void LogError(object value) => throw new InvalidOperationException(value?.ToString());
    }
    public static class Random
    {
        private static System.Random Generator = new(0);
        public static void InitState(int seed) => Generator = new(seed);
        public static float value => (float)Generator.NextDouble();
        public static int Range(int min, int max) => Generator.Next(min, max);
        public static float Range(float min, float max) => min + (max-min)*value;
    }
    public static class Mathf
    {
        public const float PI = MathF.PI, Deg2Rad = PI/180, Infinity = float.PositiveInfinity;
        public static int Max(int a,int b) => Math.Max(a,b);
        public static float Max(float a,float b) => Math.Max(a,b);
        public static int Min(int a,int b) => Math.Min(a,b);
        public static float Min(float a,float b) => Math.Min(a,b);
        public static int Clamp(int v,int min,int max) => Math.Clamp(v,min,max);
        public static float Clamp(float v,float min,float max) => Math.Clamp(v,min,max);
        public static float Clamp01(float v) => Clamp(v,0,1);
        public static int RoundToInt(float v) => (int)MathF.Round(v,MidpointRounding.ToEven);
        public static int CeilToInt(float v) => (int)MathF.Ceiling(v);
        public static int FloorToInt(float v) => (int)MathF.Floor(v);
        public static float Round(float v) => MathF.Round(v,MidpointRounding.ToEven);
        public static float Abs(float v) => MathF.Abs(v);
        public static int Abs(int v) => Math.Abs(v);
        public static float Pow(float v,float power) => MathF.Pow(v,power);
        public static float Sqrt(float v) => MathF.Sqrt(v);
        public static float Sign(float v) => v>=0f?1f:-1f;
        public static float Lerp(float a,float b,float t) => a+(b-a)*Clamp01(t);
        public static bool Approximately(float a,float b) => Abs(b-a) < Max(.000001f*Max(Abs(a),Abs(b)),float.Epsilon*8);
    }
    public struct Vector3
    {
        public float x,y,z; public Vector3(float x,float y,float z=0) { this.x=x;this.y=y;this.z=z; }
        public static Vector3 zero => new(); public static Vector3 one => new(1,1,1);
        public static Vector3 operator +(Vector3 a,Vector3 b) => new(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator -(Vector3 a,Vector3 b) => new(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator *(Vector3 a,float b) => new(a.x*b,a.y*b,a.z*b);
        public static Vector3 operator /(Vector3 a,float b) => a*(1/b);
        public float magnitude => MathF.Sqrt(x*x+y*y+z*z);
        public Vector3 normalized => magnitude>0 ? this*(1/magnitude) : zero;
        public static float Distance(Vector3 a,Vector3 b) => (a-b).magnitude;
    }
    public struct Vector2 { public float x,y; public Vector2(float x,float y) { this.x=x;this.y=y; } public static Vector2 zero => new(); public static Vector2 one => new(1,1); public static float Distance(Vector2 a,Vector2 b)=>Vector3.Distance(a,b); public static implicit operator Vector3(Vector2 v) => new(v.x,v.y); }
    public struct Color { public float r,g,b,a; public Color(float r,float g,float b,float a=1) {this.r=r;this.g=g;this.b=b;this.a=a;} public static Color white => new(1,1,1); public static Color black => new(0,0,0); public static Color Lerp(Color a,Color b,float t) => a; }
    public class TextAsset { public string text; }
    public static class ColorUtility { public static bool TryParseHtmlString(string value,out Color color) {color=default;return false;} }
    public static class Resources
    {
        public static string DataDirectory;
        public static T Load<T>(string path) where T : class => typeof(T)==typeof(TextAsset)
            ? new TextAsset { text = File.ReadAllText(Path.Combine(DataDirectory,Path.GetFileName(path)+".yaml")) } as T : null;
    }
}
