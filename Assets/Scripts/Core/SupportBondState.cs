using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// 런 범위의 서포트 우정도 상태. RunManager가 소유하며 런 시작 시 초기화되고
    /// 저장/복원 대상이다. (구현이 static 딕셔너리였을 때는 런 간 누수를 수동으로 막아야 했다.)
    /// </summary>
    public class SupportBondState
    {
        public const int MaxBond = 100;

        private readonly Dictionary<int, int> _bonds = new();

        public bool TryGet(int unitId, out int bond) => _bonds.TryGetValue(unitId, out bond);

        public void Set(int unitId, int bond)
        {
            if (unitId <= 0) return;
            _bonds[unitId] = Mathf.Clamp(bond, 0, MaxBond);
        }

        public void Clear() => _bonds.Clear();

        public void Restore(IEnumerable<SupportBondSaveData> savedBonds)
        {
            _bonds.Clear();
            if (savedBonds == null) return;

            foreach (SupportBondSaveData saved in savedBonds)
            {
                if (saved == null || saved.unitId <= 0) continue;
                _bonds[saved.unitId] = Mathf.Clamp(saved.currentBond, 0, MaxBond);
            }
        }

        public List<SupportBondSaveData> BuildSaveData()
        {
            return _bonds
                .Where(pair => pair.Key > 0)
                .Select(pair => new SupportBondSaveData
                {
                    unitId = pair.Key,
                    currentBond = Mathf.Clamp(pair.Value, 0, MaxBond),
                })
                .ToList();
        }
    }
}
