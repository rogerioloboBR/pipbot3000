// Classe estática para guardar dados de regras do livro
public class Rulebook
{
    // Dicionário para mapear perícias aos atributos S.P.E.C.I.A.L. (pág. 44)
    public readonly Dictionary<string, string> SkillToAttribute = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        {"armascorpoacorpo", "FOR"}, {"armaspesadas", "RES"}, {"desarmado", "FOR"},
        {"armasdeenergia", "PER"}, {"explosivos", "PER"}, {"arrombamento", "PER"}, {"pilotagem", "PER"},
        {"armaspequenas", "AGI"}, {"arremesso", "AGI"}, {"furtividade", "AGI"},
        {"atletismo", "FOR"}, {"barganha", "CAR"}, {"retorica", "CAR"},
        {"ciencias", "INT"}, {"medicina", "INT"}, {"reparo", "INT"},
        {"sobrevivencia", "RES"}
    };

    // GetInjuryText() e GetRuleText() foram removidos 
    // e agora são lidos do banco de dados pelo InfoService.
}