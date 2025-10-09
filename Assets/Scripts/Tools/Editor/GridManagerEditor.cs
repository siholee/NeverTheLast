using UnityEngine;
using UnityEditor;
using Managers;

namespace Tools.Editor
{
    [CustomEditor(typeof(GridManager))]
    public class GridManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            
            GridManager gridManager = (GridManager)target;
            
            GUILayout.Space(10);
            
            GUILayout.BeginHorizontal();
            
            if (GUILayout.Button("그리드 생성", GUILayout.Height(30)))
            {
                if (gridManager.cellPrefab == null)
                {
                    EditorUtility.DisplayDialog("오류", "Cell Prefab이 할당되지 않았습니다!", "확인");
                    return;
                }
                
                if (gridManager.Field == null)
                {
                    EditorUtility.DisplayDialog("오류", "Field Transform이 할당되지 않았습니다!", "확인");
                    return;
                }
                
                if (gridManager.Bench == null)
                {
                    EditorUtility.DisplayDialog("오류", "Bench Transform이 할당되지 않았습니다!", "확인");
                    return;
                }
                
                if (EditorUtility.DisplayDialog("그리드 생성", 
                    "기존 셀들을 삭제하고 새로운 그리드를 생성하시겠습니까?", 
                    "확인", "취소"))
                {
                    gridManager.RegenerateGrid();
                    EditorUtility.SetDirty(gridManager);
                }
            }
            
            if (GUILayout.Button("기존 셀 정리", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("셀 정리", 
                    "모든 기존 셀들을 삭제하시겠습니까? (실행 취소 불가)", 
                    "확인", "취소"))
                {
                    gridManager.ClearExistingCells();
                    EditorUtility.SetDirty(gridManager);
                }
            }
            
            GUILayout.EndHorizontal();
            
            GUILayout.Space(10);
            
            // 현재 그리드 상태 정보 표시
            EditorGUILayout.LabelField("그리드 정보", EditorStyles.boldLabel);
            
            if (gridManager.Field != null)
            {
                EditorGUILayout.LabelField($"Field 자식 수: {gridManager.Field.childCount}");
            }
            else
            {
                EditorGUILayout.LabelField("Field: 할당되지 않음", EditorStyles.helpBox);
            }
            
            if (gridManager.Bench != null)
            {
                EditorGUILayout.LabelField($"Bench 자식 수: {gridManager.Bench.childCount}");
            }
            else
            {
                EditorGUILayout.LabelField("Bench: 할당되지 않음", EditorStyles.helpBox);
            }
        }
    }
}