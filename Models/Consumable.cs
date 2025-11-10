public class Consumable
{
    public string Nome { get; set; } = "";
    public string PV_Curado { get; set; } = ""; // Mantido como string (ex: "6 PV")
    public string Efeito { get; set; } = "";
    public string Irradiado { get; set; } = ""; // Mantido como string (ex: "1 CD")
    public double Peso { get; set; }
    public int Custo { get; set; }
    public int Raridade { get; set; }
}
