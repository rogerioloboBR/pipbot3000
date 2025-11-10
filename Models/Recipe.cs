/// <summary>
/// Uma classe unificada para representar uma receita, 
/// seja ela de um item (tabela 'Receitas') ou de um mod (tabela 'Mods').
/// </summary>
public class Recipe
{
    public string ItemName { get; set; } = "";
    public string? Materiais { get; set; } // Null se for um Mod (calculado pela complexidade)
    public int Complexidade { get; set; }
    public string Vantagens { get; set; } = "";
    public string Pericia { get; set; } = "";
    public string Raridade { get; set; } = "";
    public int FontePagina { get; set; }
    public bool IsMod { get; set; } = false; // Flag para sabermos se é um Mod ou um Item (Receita)
}