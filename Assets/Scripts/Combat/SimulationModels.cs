using System.Collections.Generic;
using Core;

namespace NeverTheLast.Simulation
{
    public sealed class Experiment
    {
        public int Runs { get; set; }=100;
        public int Seed { get; set; }=1000;
        public int MaxActions { get; set; }=3000;
        public float MaxCycles { get; set; }=100;
        public List<Team> Teams { get; set; }=new();
        public List<Encounter> Encounters { get; set; }=new();
    }
    public sealed class Team
    {
        public string Name { get; set; }
        public int Gold { get; set; }
        public List<Member> Members { get; set; }=new();
    }
    public sealed class Member
    {
        public int Id { get; set; }
        public int? Level { get; set; }
        public int Column { get; set; }=1;
        public int Row { get; set; }=1;
        public float HpRatio { get; set; }=1;
        public int Resource { get; set; }
        public List<int> Rewards { get; set; }=new();
        public List<RewardCodeInstanceData> RewardInstances { get; set; }=new();
        public List<PermanentStackSaveData> Stacks { get; set; }=new();
        public List<PermanentStatSaveData> Nectar { get; set; }=new();
    }
    public sealed class Encounter
    {
        public string Name { get; set; }
        public int Stage { get; set; }=1;
        public string Kind { get; set; }="Normal";
        public List<Member> Enemies { get; set; }=new();
    }
    public sealed class UnitResult
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool Enemy { get; set; }
        public int Column { get; set; }
        public int Row { get; set; }
        public int Hp { get; set; }
        public int MaxHp { get; set; }
        public long Damage { get; set; }
        public long Healing { get; set; }
        public long Shielding { get; set; }
        public int Turns { get; set; }
    }
    public sealed class BattleResult
    {
        public string Team { get; set; }
        public string Encounter { get; set; }
        public int Seed { get; set; }
        public string Outcome { get; set; }
        public string Reason { get; set; }
        public string EventDigest { get; set; }
        public Team TeamDefinition { get; set; }
        public Encounter EncounterDefinition { get; set; }
        public int MaxActions { get; set; }
        public float MaxCycles { get; set; }
        public int Actions { get; set; }
        public int Turns { get; set; }
        public float ActionValue { get; set; }
        public Dictionary<string,int> ActionsByKind { get; set; }=new();
        public List<UnitResult> Units { get; set; }=new();
    }

}

