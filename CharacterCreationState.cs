public enum CreationStep
{
    Origin,
    Special,
    TagSkills,
    DistributeSkills,
    Name,
    Done
}

public class CharacterCreationState
{
    public CreationStep CurrentStep { get; set; } = CreationStep.Origin;
    public ulong GuildId { get; set; }
    public string GuildName { get; set; } = "";
    public string Origin { get; set; } = "";
    public int[] Special { get; set; } = new int[7];
    public Dictionary<string, int> Skills { get; set; } = new Dictionary<string, int>();
    public List<string> TagSkills { get; set; } = new List<string>();
    public string Name { get; set; } = "";
}