namespace BaseClasses
{
    [System.Serializable]
    public class UnitInfo
    {
        public string Name { get; private set; }
        public string PortraitPath { get; private set; }

        public UnitInfo(string name, string portraitPath)
        {
            Name = name;
            PortraitPath = portraitPath;
        }
    }
}
