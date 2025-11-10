public class CreatureNPCStats
{
    // Adiciona System.Text para que o DatabaseService possa usá-lo
    public string Nome { get; set; } = "";
    public int Nivel { get; set; }
    public string Tipo { get; set; } = "";
    public string PalavrasChave { get; set; } = "";

    // Atributos (S.P.E.C.I.A.L. ou Corpo/Mente)
    public int FOR_Val { get; set; }
    public int PER_Val { get; set; }
    public int RES_Val { get; set; }
    public int CAR_Val { get; set; }
    public int INT_Val { get; set; }
    public int AGI_Val { get; set; }
    public int SOR_Val { get; set; }

    // Estatísticas Derivadas/Finais
    public int PV_Base { get; set; }
    public int Iniciativa_Base { get; set; }
    public int Defesa { get; set; }
    public int RD_Fisico_Base { get; set; }
    public int RD_Energetico_Base { get; set; }
    public string RD_Radiativo_Base { get; set; } = "";
    public string RD_Venenoso_Base { get; set; } = "";

    // Ataques e Detalhes
    public string Ataques { get; set; } = "";
    public string Inventario { get; set; } = "";
    public string Habilidades_Especiais { get; set; } = "";
    public int FontePagina { get; set; }
}