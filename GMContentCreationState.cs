public enum GMCreateStep
{
    Type,           // Etapa 1 - Tipo de conteúdo (NPC ou Criatura)
    Name,           // Etapa 2 - Nome do conteúdo
    LevelAndType,   // Etapa 3 - Nível e tipo (Normal, Notável, Principal, etc.)
    Attributes,     // Etapa 4 - Atributos (S.P.E.C.I.A.L. ou similares)
    Keywords,       // Etapa 5 - Palavras-chave
    Finalize        // Etapa 6 - Finalização e salvamento
}

public class GMContentCreationState
{
    public GMCreateStep CurrentStep { get; set; } = GMCreateStep.Type;

    public ulong GuildId { get; set; }
    public string GuildName { get; set; } = "";

    public string Name { get; set; } = "";
    public int Level { get; set; }
    public string Type { get; set; } = ""; // Normal, Notável, Principal, Poderosa, Lendária

    // Atributos básicos (S.P.E.C.I.A.L.)
    public Dictionary<string, int> Attributes { get; set; } = new Dictionary<string, int>();

    // Palavras-chave (traits, talentos, etc.)
    public List<string> Keywords { get; set; } = new List<string>();
}