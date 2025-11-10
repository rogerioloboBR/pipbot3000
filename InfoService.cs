using Discord;
using Discord.WebSocket;
using System.Text;

public class InfoService
{
    private readonly Rulebook _rulebook;
    private readonly DatabaseService _dbService;
    private readonly Random _random = new Random();

    public InfoService(DatabaseService dbService, Rulebook rulebook)
    {
        _dbService = dbService;
        _rulebook = rulebook;
    }

    // ### LÓGICA DO !ferimento ###
    public async Task HandleInjuryCommandAsync(SocketMessage message)
    {
        string[] parts = message.Content.Split(' ');
        string location = parts.Length > 1 ? parts[1].ToLower() : "aleatorio";

        string locationKey = location;
        string locationName = "";

        if (locationKey == "aleatorio")
        {
            int roll = _random.Next(1, 21);
            locationName = $"Aleatório (Rolagem: {roll}) - ";

            if (roll <= 2) { locationKey = "cabeca"; }
            else if (roll <= 8) { locationKey = "tronco"; }
            else if (roll <= 11) { locationKey = "braco_esq"; }
            else if (roll <= 14) { locationKey = "braco_dir"; }
            else if (roll <= 17) { locationKey = "perna_esq"; }
            else { locationKey = "perna_dir"; }
        }

        var (title, text, page) = await _dbService.GetInjuryAsync(locationKey);

        if (title == null)
        {
            title = "Local Desconhecido";
            text = "Use: `!ferimento cabeca`, `!ferimento tronco`, `!ferimento braco`, `!ferimento perna` ou `!ferimento aleatorio`.";
            page = 0;
        }

        var embed = new EmbedBuilder()
            .WithTitle($"🩸 Ferimento: {locationName}{title}")
            .WithDescription(text)
            .WithColor(Color.DarkRed)
            .WithFooter(page > 0 ? $"Fonte: Livro de Regras, pág. {page}" : " ")
            .Build();

        await message.Channel.SendMessageAsync(embed: embed);
    }

    // ### LÓGICA DO !regra ###
    public async Task HandleRuleQueryCommandAsync(SocketMessage message)
    {
        string[] parts = message.Content.Split(' ');
        if (parts.Length < 2)
        {
            await message.Channel.SendMessageAsync("Formato inválido. Use: `!regra [termo]`");
            return;
        }

        string query = string.Join(" ", parts.Skip(1)).ToLower();

        var (title, text, page) = await _dbService.GetRuleAsync(query);

        if (title == null)
        {
            title = "Regra não encontrada";
            text = "Não encontrei uma regra para esse termo.\n*(Tente palavras-chave como `stimpak`, `agonizando`, `cobertura`, etc.)*";
            page = 0;
        }

        var embed = new EmbedBuilder()
            .WithTitle($"📖 Regra: {title}")
            .WithDescription(text)
            .WithColor(Color.LightGrey)
            .WithFooter(page > 0 ? $"Fonte: Livro de Regras, pág. {page}" : " ")
            .Build();

        await message.Channel.SendMessageAsync(embed: embed);
    }

    // ### LÓGICA DO !item ###
    public async Task HandleItemQueryAsync(SocketMessage message)
    {
        string query = message.Content.Substring(message.Content.IndexOf(' ') + 1).Trim();

        if (string.IsNullOrWhiteSpace(query))
        {
            await message.Channel.SendMessageAsync("Formato inválido. Use: `!item [nome do item]`");
            return;
        }

        var weapon = await _dbService.GetWeaponAsync(query);
        if (weapon != null)
        {
            await message.Channel.SendMessageAsync(embed: BuildWeaponEmbed(weapon));
            return;
        }

        var armor = await _dbService.GetArmorAsync(query);
        if (armor != null)
        {
            await message.Channel.SendMessageAsync(embed: BuildArmorEmbed(armor));
            return;
        }

        var consumable = await _dbService.GetConsumableAsync(query);
        if (consumable != null)
        {
            await message.Channel.SendMessageAsync(embed: BuildConsumableEmbed(consumable));
            return;
        }

        await message.Channel.SendMessageAsync($"Item não encontrado para: `{query}`. (Nota: Mods de itens são buscados com `!mod`)");
    }

    // ### LÓGICA DO !mod ###
    public async Task HandleModQueryAsync(SocketMessage message)
    {
        string query = message.Content.Substring(message.Content.IndexOf(' ') + 1).Trim();

        if (string.IsNullOrWhiteSpace(query))
        {
            await message.Channel.SendMessageAsync("Formato inválido. Use: `!mod [nome do mod]`");
            return;
        }

        var mod = await _dbService.GetModAsync(query);
        if (mod != null)
        {
            await message.Channel.SendMessageAsync(embed: BuildModEmbed(mod));
            return;
        }

        await message.Channel.SendMessageAsync($"Modificação não encontrada para: `{query}`.");
    }

    // ### LÓGICA DO !vasculhar / !loot ###
    public async Task HandleLootCommandAsync(SocketMessage message)
    {
        string[] parts = message.Content.Split(' ');
        if (parts.Length < 2)
        {
            await message.Channel.SendMessageAsync("Formato inválido. Use: `!vasculhar [categoria] [quantidade (opcional)]`\nCategorias: `municao`, `armadura`, `roupa`, `comida`, `bebida`, `narco`, `arma_d`, `arma_c`, `explosivo`, `curiosidade`, `forragem`, `nuka`");
            return;
        }

        string categoria = parts[1].ToLower();
        int quantidade = 1;
        if (parts.Length > 2 && int.TryParse(parts[2], out int qty))
        {
            quantidade = Math.Clamp(qty, 1, 20);
        }

        var (tableName, diceType) = GetLootTableDetails(categoria);

        if (tableName == null)
        {
            await message.Channel.SendMessageAsync("Categoria de pilhagem inválida. Use: `municao`, `armadura`, `roupa`, `comida`, `bebida`, `narco`, `arma_d`, `arma_c`, `explosivo`, `curiosidade`, `forragem`, `nuka`");
            return;
        }

        List<string> results = new List<string>();
        for (int i = 0; i < quantidade; i++)
        {
            int roll = RollDice(diceType);
            var item = await _dbService.GetLootResultAsync(tableName, roll);

            if (item != null)
            {
                var existing = results.FirstOrDefault(r => r.Contains(item.ItemName));
                if (existing != null && item.Quantidade == "1")
                {
                    int index = results.IndexOf(existing);
                    int currentCount = int.Parse(existing.Split('x')[0]);
                    results[index] = $"{currentCount + 1}x {item.ItemName}";
                }
                else
                {
                    results.Add(item.Quantidade == "1" ? $"1x {item.ItemName}" : $"1x {item.ItemName} (Qtd: {item.Quantidade})");
                }
            }
        }

        if (results.Count == 0)
        {
            await message.Channel.SendMessageAsync($"Nenhum item encontrado para `{categoria}` (rolagem de dados falhou ou tabela vazia?).");
            return;
        }

        string resultText = "```" + string.Join("\n", results) + "```";
        if (resultText.Length > 4000)
        {
            resultText = "```Muitos itens encontrados! (O resultado excedeu o limite de caracteres do Discord).```";
        }

        var embed = new EmbedBuilder()
            .WithTitle($"🔍 Pilhagem Encontrada: {categoria} (x{quantidade})")
            .WithColor(Color.LightGrey)
            .WithDescription(resultText)
            .WithFooter($"Rolagens na tabela da pág. {GetLootPage(categoria)}");

        await message.Channel.SendMessageAsync(embed: embed.Build());
    }

    // ### LÓGICA DO !npc (NOVO MÉTODO) ###
    public async Task HandleNPCQueryAsync(SocketMessage message)
    {
        string query = message.Content.Substring(message.Content.IndexOf(' ') + 1).Trim();

        if (string.IsNullOrWhiteSpace(query))
        {
            await message.Channel.SendMessageAsync("Formato inválido. Use: `!npc [nome da criatura/NPC]` (Ex: `!npc mirelurk`)");
            return;
        }

        var stats = await _dbService.GetCreatureStatsAsync(query);
        if (stats == null)
        {
            await message.Channel.SendMessageAsync($"Criatura/NPC não encontrado para: `{query}`.");
            return;
        }

        await message.Channel.SendMessageAsync(embed: BuildNPCEmbed(stats));
    }


    // --- Métodos Auxiliares de Pilhagem ---

    private (string? TableName, string DiceType) GetLootTableDetails(string categoria)
    {
        switch (categoria)
        {
            case "municao": return ("Loot_Municao", "2d20");
            case "armadura": return ("Loot_Armadura", "2d20");
            case "roupa": return ("Loot_Roupa", "2d20");
            case "comida": return ("Loot_Comida", "2d20");
            case "bebida": return ("Loot_Bebida", "2d20");
            case "narco": return ("Loot_Narco", "2d20");
            case "arma_d":
            case "arma-d":
            case "arma_distancia":
                return ("Loot_Arma_Distancia", "2d20");
            case "arma_c":
            case "arma-c":
            case "arma_corpo":
                return ("Loot_Arma_CorpoACorpo", "2d20");
            case "explosivo":
            case "explosivos":
                return ("Loot_Explosivos", "2d20");
            case "curiosidade":
            case "curiosidades":
                return ("Loot_Curiosidades", "3d20");
            case "forragem":
            case "forrageamento":
                return ("Loot_Forrageamento", "1d20");
            case "nuka":
            case "nukacola":
                return ("Loot_NukaCola", "1d20");
            default:
                return (null, "");
        }
    }

    private int RollDice(string diceType)
    {
        switch (diceType)
        {
            case "1d20":
                return _random.Next(1, 21);
            case "2d20":
                return _random.Next(1, 21) + _random.Next(1, 21);
            case "3d20":
                return _random.Next(1, 21) + _random.Next(1, 21) + _random.Next(1, 21);
            default:
                return 0;
        }
    }

    private int GetLootPage(string categoria)
    {
        switch (categoria)
        {
            case "municao": return 200;
            case "armadura": return 201;
            case "roupa": return 201;
            case "comida": return 202;
            case "forragem": return 202;
            case "bebida": return 203;
            case "nuka": return 203;
            case "narco": return 204;
            case "arma_d": return 204;
            case "arma_c": return 205;
            case "explosivo": return 205;
            case "curiosidade": return 207;
            default: return 200;
        }
    }


    // --- Métodos Auxiliares de Embed ---

    private Embed BuildWeaponEmbed(Weapon weapon)
    {
        var embed = new EmbedBuilder()
            .WithTitle($"🔫 Arma: {weapon.Nome}")
            .WithColor(Color.Orange)
            .AddField("Perícia", weapon.Pericia, inline: true)
            .AddField("Dano", weapon.Dano, inline: true)
            .AddField("Tipo", weapon.TipoDano, inline: true)
            .AddField("Efeitos", string.IsNullOrWhiteSpace(weapon.Efeitos) ? "Nenhum" : weapon.Efeitos)
            .AddField("Qualidades", string.IsNullOrWhiteSpace(weapon.Qualidades) ? "Nenhuma" : weapon.Qualidades)
            .AddField("CdT", weapon.CdT, inline: true)
            .AddField("Dist.", weapon.Distancia, inline: true)
            .AddField("Peso", $"{weapon.Peso} kg", inline: true)
            .AddField("Custo", $"{weapon.Custo} tampas", inline: true)
            .AddField("Raridade", weapon.Raridade, inline: true)
            .WithFooter("Fonte: Livro de Regras (Tabelas de Armas)");

        return embed.Build();
    }

    private Embed BuildArmorEmbed(Armor armor)
    {
        var embed = new EmbedBuilder()
            .WithTitle($"🛡️ Armadura: {armor.Nome}")
            .WithColor(Color.Blue);

        if (armor.PV.HasValue && armor.PV > 0)
        {
            embed.AddField("Tipo", "Armadura Potente", inline: true);
            embed.AddField("PV da Peça", armor.PV.Value, inline: true);
        }

        embed.AddField("Área Coberta", armor.AreaCoberta, inline: true);
        embed.AddField("RD Físico", armor.RD_Fisico, inline: true);
        embed.AddField("RD Energético", armor.RD_Energetico, inline: true);
        embed.AddField("RD Radiativo", armor.RD_Radiativo, inline: true);
        embed.AddField("Peso", $"{armor.Peso} kg", inline: true);
        embed.AddField("Custo", $"{armor.Custo} tampas", inline: true);
        embed.AddField("Raridade", armor.Raridade, inline: true)
             .WithFooter("Fonte: Livro de Regras (Tabelas de Armaduras)");

        return embed.Build();
    }

    private Embed BuildConsumableEmbed(Consumable consumable)
    {
        var embed = new EmbedBuilder()
            .WithTitle($"💊 Consumível: {consumable.Nome}")
            .WithColor(Color.Green)
            .AddField("PV Curado", consumable.PV_Curado, inline: true)
            .AddField("Irradiado?", consumable.Irradiado, inline: true)
            .AddField("Efeito", string.IsNullOrWhiteSpace(consumable.Efeito) ? "Nenhum" : consumable.Efeito)
            .AddField("Peso", $"{consumable.Peso} kg", inline: true)
            .AddField("Custo", $"{consumable.Custo} tampas", inline: true)
            .AddField("Raridade", consumable.Raridade, inline: true)
            .WithFooter("Fonte: Livro de Regras (Tabelas de Consumíveis)");

        return embed.Build();
    }

    private Embed BuildModEmbed(Mod mod)
    {
        var embed = new EmbedBuilder()
            .WithTitle($"🔧 Mod: {mod.Nome}")
            .WithColor(new Color(0xAAAAAA)) // Cinza
            .WithDescription($"**Tipo:** {mod.TipoMod}");

        embed.AddField("Efeitos", mod.Efeitos);

        StringBuilder requirements = new StringBuilder();
        requirements.AppendLine($"**Perícia:** {mod.Pericia}");
        if (!string.IsNullOrWhiteSpace(mod.Vantagens) && mod.Vantagens != "-")
        {
            requirements.AppendLine($"**Vantagens:** {mod.Vantagens}");
        }
        requirements.AppendLine($"**Raridade:** {mod.Raridade}");

        embed.AddField("Requisitos de Fabricação", requirements.ToString());

        embed.AddField("Mod de Peso", $"{mod.PesoMod:+#.#;-#.#;0} kg", inline: true);
        embed.AddField("Custo", $"{mod.CustoMod} tampas", inline: true)
             .WithFooter($"Fonte: Livro de Regras, pág. {mod.FontePagina}");

        return embed.Build();
    }

    private Embed BuildNPCEmbed(CreatureNPCStats stats)
    {
        Color color = stats.Tipo switch
        {
            "Normal" => Color.LightGrey,
            "Notável" => Color.Blue,
            "Principal" => Color.Gold,
            _ => Color.DarkRed
        };

        string attributes = stats.Tipo.Contains("Normal") ?
            $"FOR (Corpo): {stats.FOR_Val} | INT (Mente): {stats.INT_Val}" :
            $"FOR: {stats.FOR_Val} | PER: {stats.PER_Val} | RES: {stats.RES_Val}\nCAR: {stats.CAR_Val} | INT: {stats.INT_Val} | AGI: {stats.AGI_Val} | SOR: {stats.SOR_Val}";

        string rds = $"Físico: {stats.RD_Fisico_Base} | Energético: {stats.RD_Energetico_Base}\nRadiativo: {stats.RD_Radiativo_Base} | Venenoso: {stats.RD_Venenoso_Base}";

        var embed = new EmbedBuilder()
            .WithTitle($"🧟‍♂️ Ficha - {stats.Nome} (Nível {stats.Nivel})")
            .WithColor(color)
            .AddField("Tipo", $"{stats.Tipo} ({stats.PalavrasChave})")
            .AddField("Atributos", attributes)
            .AddField("PV / Iniciativa / Defesa", $"PV: **{stats.PV_Base}** | Iniciativa: **{stats.Iniciativa_Base}** | Defesa: **{stats.Defesa}**", inline: true)
            .AddField("Redutores de Dano (RD)", rds, inline: true)
            .AddField("Ataques", stats.Ataques.Replace(",", "\n"))
            .AddField("Habilidades Especiais", string.IsNullOrWhiteSpace(stats.Habilidades_Especiais) ? "Nenhuma" : stats.Habilidades_Especiais)
            .AddField("Inventário / Abate", stats.Inventario)
            .WithFooter($"Fonte: Capítulo Dez, pág. {stats.FontePagina}");

        return embed.Build();
    }
    /// <summary>
    /// Rola 1d20 para determinar a área de acerto no combate, conforme a pág. 28.
    /// </summary>
    public async Task HandleAreaRollCommandAsync(SocketMessage message)
    {
        int roll = _random.Next(1, 21);
        string area = "";
        string emoji = "";

        // Tabela de Áreas de Acerto (pág. 28)
        if (roll <= 2)
        {
            area = "Cabeça";
            emoji = "🧠";
        }
        else if (roll <= 8)
        {
            area = "Tronco";
            emoji = "🛡️";
        }
        else if (roll <= 11)
        {
            area = "Braço Esquerdo";
            emoji = "🦾";
        }
        else if (roll <= 14)
        {
            area = "Braço Direito";
            emoji = "🦾";
        }
        else if (roll <= 17)
        {
            area = "Perna Esquerda";
            emoji = "🦵";
        }
        else // 18 a 20
        {
            area = "Perna Direita";
            emoji = "🦵";
        }

        var embed = new EmbedBuilder()
            .WithTitle($"🎯 Rolagem de Área de Acerto (1d20)")
            .WithDescription($"O ataque acertou a área:\n\n# {emoji} **{area}**")
            .AddField("Rolagem", $"` {roll} `", true)
            .AddField("Faixa", $"1-{roll}", true)
            .WithColor(Color.Orange)
            .WithFooter("Fonte: Livro de Regras, Tabela de Áreas de Acerto (pág. 28)")
            .Build();

        await message.Channel.SendMessageAsync(embed: embed);
    }
    /// <summary>
    /// Fornece um resumo de ferramentas e dados críticos do GM (XP, NPC, PA, etc.).
    /// </summary>
    public async Task HandleGMToolkitCommandAsync(SocketMessage message, ulong guildId)
    {
        // 1. Obter status de PA
        int currentPA = await _dbService.GetActionPointsAsync(guildId);

        // 2. Obter lista de personagens e seus níveis/XP para a gestão de XP
        var characters = await _dbService.GetAllCharactersInGuildAsync(guildId);

        var xpSummary = new StringBuilder();
        if (characters.Any())
        {
            // Limita a exibição para que o embed não fique muito grande
            foreach (var c in characters.Take(5))
            {
                xpSummary.AppendLine($"`{c.Name}`: Nvl {c.Level} ({c.XP} XP)");
            }
            if (characters.Count > 5)
            {
                xpSummary.AppendLine($"*...e mais {characters.Count - 5} personagens.*");
            }
        }
        else
        {
            xpSummary.AppendLine("Nenhum PJ registrado.");
        }

        var embed = new EmbedBuilder()
            .WithTitle("🧰 Kit de Ferramentas do Mestre de Jogo (GM Toolkit)")
            .WithDescription("Este é o seu centro de comando para gerenciar a sessão.")
            .WithColor(Color.DarkPurple)
            .AddField("Pontos de Ação (PA) do Grupo",
                      $"O grupo tem **{currentPA} / 6 PA**.\n" +
                      "`!pa [ver/add/gastar/set] [valor]`",
                      true)
            .AddField("Ferramentas de Combate",
                      "Gerencie a ordem de iniciativa:\n" +
                      "`!combate iniciar`\n" +
                      "`!combate add [NPC] [INIT]`\n" +
                      "`!combate proximo`",
                      true)
            .AddField("Gerenciamento de XP e Nível",
                      xpSummary.ToString(),
                      false)
            .AddField("Cálculo Rápido de XP",
                      "Use o nome do NPC/Criatura para calcular o XP total para o grupo:\n" +
                      "`!xp derrota [nome_npc]`",
                      true)
            .AddField("Consultas Rápidas",
                      "`!npc [nome]` (Ficha NPC)\n" +
                      "`!regra [termo]` (Regras e Mecânicas)\n" +
                      "`!vasculhar [cat]` (Tabela de Pilhagem)\n" +
                      "`!area` (Rolar Área de Acerto)",
                      true)
            .WithFooter($"Use os comandos acima para preparar e gerenciar o jogo.");

        await message.Channel.SendMessageAsync(embed: embed.Build());
    }
}