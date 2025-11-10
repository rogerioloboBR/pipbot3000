public class Armor
{
    public string Nome { get; set; } = "";
    public string AreaCoberta { get; set; } = "";
    public int RD_Fisico { get; set; }
    public int RD_Energetico { get; set; }
    public int RD_Radiativo { get; set; }
    public int? PV { get; set; } // Nullable, pois só Armadura Potente tem PV
    public double Peso { get; set; }
    public int Custo { get; set; }
    public int Raridade { get; set; }
}