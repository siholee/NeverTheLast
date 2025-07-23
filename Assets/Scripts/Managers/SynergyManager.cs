using System;
using System.Collections.Generic;
using BaseClasses;
using UnityEngine;
using static BaseClasses.BaseEnums;
using YamlDotNet.Serialization;
using Managers.UI;

namespace Managers
{
    public class SynergyManager : MonoBehaviour
    {
        public GameObject synergyTagPrefab;
        public GameObject synergyDisplay;
        public List<Synergy> synergyList = new List<Synergy>();
        public List<SynergyTag> synergyTagList = new List<SynergyTag>();

        // Start에서 초기 UI 설정
        void Start()
        {
            LoadSynergiesFromYaml();
            CreateSynergyTags();
        }

        void LoadSynergiesFromYaml()
        {
            // Resources/Data/40_synergies.yaml 파일 로드
            TextAsset yamlFile = Resources.Load<TextAsset>("Data/40_synergies");
            if (yamlFile == null)
            {
                Debug.LogError("40_synergies.yaml 파일을 찾을 수 없습니다.");
                return;
            }

            var deserializer = new DeserializerBuilder().Build();
            var synergyDataList = deserializer.Deserialize<SynergyDataList>(yamlFile.text);

            // Synergy 객체들을 생성하여 리스트에 추가
            foreach (var synergyData in synergyDataList.synergies)
            {
                GameObject synergyObj = new GameObject($"Synergy_{synergyData.name}");
                synergyObj.transform.SetParent(transform);
                
                Synergy synergy = synergyObj.AddComponent<Synergy>();
                synergy.synergyName = synergyData.name;
                synergy.synergyDescription = synergyData.description;
                synergy.synergyLevel = 0;
                synergy.synergyUpgradeCost = 0;
                synergy.isActive = false;

                synergyList.Add(synergy);
            }

            Debug.Log($"총 {synergyList.Count}개의 시너지를 로드했습니다.");
        }

        void CreateSynergyTags()
        {
            if (synergyTagPrefab == null || synergyDisplay == null)
            {
                Debug.LogError("SynergyTag Prefab 또는 SynergyDisplay가 설정되지 않았습니다.");
                return;
            }

            // 10개의 SynergyTag 생성
            for (int i = 0; i < 10; i++)
            {
                GameObject tagObj = Instantiate(synergyTagPrefab, synergyDisplay.transform);
                SynergyTag synergyTag = tagObj.GetComponent<SynergyTag>();
                
                if (synergyTag == null)
                {
                    Debug.LogError("SynergyTag 컴포넌트를 찾을 수 없습니다.");
                    continue;
                }

                // RectTransform 설정
                RectTransform rectTransform = tagObj.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    // 각 태그의 위치 설정 (70픽셀씩 아래로)
                    float topPosition = 30 + (i * 70);
                    float bottomPosition = 700 - (i * 70);
                    
                    rectTransform.anchorMin = new Vector2(0, 0);
                    rectTransform.anchorMax = new Vector2(1, 1);
                    rectTransform.offsetMin = new Vector2(10, bottomPosition);
                    rectTransform.offsetMax = new Vector2(-10, -topPosition);
                    rectTransform.anchoredPosition3D = new Vector3(0, 0, 0);
                }

                // 처음에는 비활성화
                synergyTag.SetActive(false);
                synergyTagList.Add(synergyTag);
            }

            Debug.Log($"총 {synergyTagList.Count}개의 SynergyTag UI를 생성했습니다.");
        }

        public void SynergyUpdate()
        {
            // 우선 모든 태그 비활성화
            foreach (var tag in synergyTagList)
            {
                tag.SetActive(false);
            }

            // 활성화된 시너지 찾기
            List<Synergy> activeSynergies = new List<Synergy>();
            foreach (var synergy in synergyList)
            {
                if (synergy.isActive)
                {
                    activeSynergies.Add(synergy);
                }
            }

            // 활성화된 시너지 수만큼 태그 활성화 및 내용 채우기
            for (int i = 0; i < activeSynergies.Count && i < synergyTagList.Count; i++)
            {
                Synergy activeSynergy = activeSynergies[i];
                SynergyTag tag = synergyTagList[i];

                tag.SetActive(true);
                
                // 내용 설정
                if (tag.synergyNameText != null)
                    tag.synergyNameText.text = activeSynergy.synergyName;
                
                if (tag.synergyDescriptionText != null)
                    tag.synergyDescriptionText.text = activeSynergy.synergyDescription;
                
                if (tag.synergyCountText != null)
                    tag.synergyCountText.text = activeSynergy.synergyLevel.ToString();
            }

            Debug.Log($"{activeSynergies.Count}개의 활성 시너지를 UI에 표시했습니다.");
        }
    }
}