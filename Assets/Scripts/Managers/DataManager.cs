using UnityEngine;
using YamlDotNet.Serialization;

namespace Managers
{
    /// <summary>
    /// Resources/Data/*.yaml 로더. DTO 클래스들은 Assets/Scripts/Data/에 있다.
    /// </summary>
    public class DataManager : MonoBehaviour
    {
        /// <summary>Resources 경로의 YAML을 T로 역직렬화한다. 파일이 없으면 null.</summary>
        private static T Load<T>(string resourcePath) where T : class
        {
            TextAsset asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null)
            {
                Debug.LogError($"{resourcePath}.yaml not found.");
                return null;
            }

            var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
            return deserializer.Deserialize<T>(asset.text);
        }

        public UnitDataList FetchUnitDataList() => Load<UnitDataList>("Data/10_units");

        public ItemDataList FetchItemDataList() => Load<ItemDataList>("Data/40_items");

        public ResourceTokenDataList FetchTokenDataList()
        {
            ResourceTokenDataList tokenDataList = Load<ResourceTokenDataList>("Data/50_tokens");
            if (tokenDataList is { tokens: not null }) return tokenDataList;
            Debug.LogError("Failed to deserialize ResourceTokenDataList.");
            return null;
        }

        public EnemyDataList FetchEnemyDataList() => Load<EnemyDataList>("Data/60_enemies");

        public RoundTypeDataList FetchRoundTypeDataList() => Load<RoundTypeDataList>("Data/70_rounds");

        public StageThemeDataList FetchStageThemeDataList() => Load<StageThemeDataList>("Data/80_stages");

        public RewardDataList FetchRewardDataList() => Load<RewardDataList>("Data/90_rewards");
    }
}
