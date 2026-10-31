using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace Managers
{
    /// <summary>파일 호스트. 게임과 동일한 DTO·YamlDotNet을 사용하고 한 데이터 세트를 실행 내내 고정한다.</summary>
    public sealed class DataManager
    {
        public static string DirectoryPath;
        private readonly Dictionary<Type,object> Cache=new();
        private T Load<T>(string name) where T:class
        {
            if(Cache.TryGetValue(typeof(T),out var cached)) return (T)cached;
            string path=Path.Combine(DirectoryPath,name+".yaml");
            if(typeof(T)==typeof(UnitDataList)) ValidateFields<UnitData>(path,"units",new[]{"startingItemIds"});
            if(typeof(T)==typeof(EnemyDataList)) ValidateFields<EnemyData>(path,"enemies",Array.Empty<string>());
            var value=new DeserializerBuilder().IgnoreUnmatchedProperties().Build().Deserialize<T>(File.ReadAllText(path));
            if(value==null) throw new InvalidDataException(path+": 비어 있는 데이터");
            Cache[typeof(T)]=value; return value;
        }
        public UnitDataList FetchUnitDataList()=>Load<UnitDataList>("10_units");
        public EnemyDataList FetchEnemyDataList()=>Load<EnemyDataList>("60_enemies");
        public SynergyDataList FetchSynergyDataList()=>Load<SynergyDataList>("30_synergies");
        private static void ValidateFields<T>(string path,string collection,string[] ignored)
        {
            var yaml=new YamlStream();using(var reader=File.OpenText(path))yaml.Load(reader);
            var root=yaml.Documents[0].RootNode as YamlMappingNode??throw new InvalidDataException("YAML 루트 오류: "+path);
            if(!root.Children.TryGetValue(new YamlScalarNode(collection),out var list)||list is not YamlSequenceNode sequence)throw new InvalidDataException("YAML 목록 오류: "+path);
            foreach(var entry in sequence.Children.Cast<YamlMappingNode>())
                ValidateNode(typeof(T),entry,path,ignored);
        }
        private static void ValidateNode(Type type,YamlNode node,string path,string[] ignored)
        {
            if(node is YamlSequenceNode sequence&&type.IsGenericType)
            {
                foreach(var child in sequence.Children)ValidateNode(type.GetGenericArguments()[0],child,path,Array.Empty<string>());
                return;
            }
            if(node is not YamlMappingNode mapping)return;
            if(type.IsGenericType&&type.GetGenericTypeDefinition()==typeof(Dictionary<,>))
            {
                foreach(var value in mapping.Children.Values)ValidateNode(type.GetGenericArguments()[1],value,path,Array.Empty<string>());
                return;
            }
            var fields=type.GetFields(BindingFlags.Public|BindingFlags.Instance).ToDictionary(f=>f.Name,StringComparer.Ordinal);
            foreach(var pair in mapping.Children)
            {
                var key=(YamlScalarNode)pair.Key;
                if(ignored.Contains(key.Value))continue;
                if(!fields.TryGetValue(key.Value,out var field))throw new InvalidDataException($"{path}:{key.Start.Line} 알 수 없는 데이터 필드: {key.Value}");
                if(key.Value=="codes"&&pair.Value is YamlMappingNode codes)
                    foreach(var code in codes.Children.Keys.Cast<YamlScalarNode>())
                        if(!new[]{"passive","normal","special","ultimate"}.Contains(code.Value))throw new InvalidDataException($"{path}:{code.Start.Line} 알 수 없는 코드 슬롯: {code.Value}");
                ValidateNode(field.FieldType,pair.Value,path,Array.Empty<string>());
            }
        }
    }
}
