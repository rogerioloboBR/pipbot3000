public class Mod
{
    public string Nome { get; set; } = "";
    public string Efeitos { get; set; } = "";
    public double PesoMod { get; set; }
    public int CustoMod { get; set; }
    public string Vantagens { get; set; } = ""; // Requisitos de Vantagem (Perk)
    public string Pericia { get; set; } = "";   // Perícia de fabricação (Reparo ou Ciências)
    public string Raridade { get; set; } = ""; // Raridade de fabricação (Comum, Incomum, Raro)
    public string TipoMod { get; set; } = "";  // Tipo (Caixa, Cano, Material, Sistema, etc.)
    public int FontePagina { get; set; }
}