using System;
using System.Collections.Generic;

namespace Managers
{
    [Serializable]
    public class ResourceTokenData
    {
        public int id;
        public string name;
    }

    [Serializable]
    public class ResourceTokenDataList
    {
        public List<ResourceTokenData> tokens;
    }
}
