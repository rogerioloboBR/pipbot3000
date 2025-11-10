using Discord;
using Discord.WebSocket;
using System.Text;
using System.Text.RegularExpressions;

public class GameService
{
    private readonly DatabaseService _dbService;
    private readonly Rulebook _rulebook;
    private readonly Random _random = new Random();
    private readonly Dictionary<ulong, UserLastRoll> _lastRollCache = new Dictionary<ulong, UserLastRoll>();
    private const int MAX_ACTION_POINTS = 6;

    public GameService(DatabaseService dbService, Rulebook rulebook)
    {
        _dbService = dbService;
        _rulebook = rulebook;
    }

    #region Handlers de Comandos de Jogo

    // O Handler principal (para !teste) agora é um 'wrapper'
    public async Task HandleTestCommandAsync(SocketUserMessage message, ulong guildId, ulong userId)
    {
        string content = message.Content;
        bool useLuck = false;
        int diceToBuy = 0;

        // 1. EXTRAIR FLAGS
        if (content.Contains("--sorte"))
        {
            useLuck = true;
            content = content.Replace("--sorte", "").Trim();
        }
        var diceRegex = @"--dados\s+(\d)";
        var diceMatch = Regex.Match(content, diceRegex);
        if (diceMatch.Success)
        {
            diceToBuy = int.Parse(diceMatch.Groups[1].Value);
            content = Regex.Replace(content, diceRegex, "").Trim();
        }

        // 2. EXTRAIR ARGUMENTOS PRINCIPAIS
        string[] parts = content.Split(' ');
        if (parts.Length < 3 || !int.TryParse(parts[2], out int difficulty))
        {
            await message.Channel.SendMessageAsync("Formato inválido. Use: `!teste [pericia] [dificuldade]`\nOpcionais: `--sorte` (custa 1 PS) ou `--dados [1-3]` (custa PAs do grupo).");
            return;
        }
        string skillName = parts[1].ToLower();

        // 3. CHAMAR O MÉTODO DE LÓGICA REUTILIZÁVEL
        var result = await GetTestResultEmbedAsync(guildId, userId, message.Author.Username, skillName, difficulty, useLuck, diceToBuy, 0);

        // 4. TRATAR O RESULTADO
        if (result.Embed == null)
        {
            await message.Channel.SendMessageAsync(result.ErrorMessage);
            return;
        }

        var sentMessage = await message.Channel.SendMessageAsync(embed: result.Embed);

        // 5. Salvar a rolagem no cache para !rerrolar
        if (result.RollCache != null)
        {
            result.RollCache.OriginalMessage = sentMessage;
            _lastRollCache[userId] = result.RollCache;
        }
    }

    // NOVO MÉTODO REUTILIZÁVEL (para !teste e /teste)
    public async Task<(Embed? Embed, string? ErrorMessage, UserLastRoll? RollCache)> GetTestResultEmbedAsync(
        ulong guildId, ulong userId, string username, string skillName, int difficulty, bool useLuck, int diceToBuy, int rerollCount)
    {
        int costInAP = 0;
        string apSpendMessage = "";
        string luckSpendMessage = "";

        if (diceToBuy < 0 || diceToBuy > 3) { return (null, "Erro: Você só pode comprar de 1 a 3 dados extras.", null); }
        costInAP = diceToBuy switch { 1 => 1, 2 => 3, 3 => 6, _ => 0 };

        if (!_rulebook.SkillToAttribute.TryGetValue(skillName, out string attributeName)) { return (null, $"Perícia `{skillName}` desconhecida.", null); }

        var characterStats = await _dbService.GetCharacterAsync(guildId, userId);
        if (characterStats == null) { return (null, "Você não tem um personagem registrado. Use `!registrar` ou `!criar-personagem` primeiro.", null); }

        var skillInfo = await _dbService.GetCharacterSkillAsync(guildId, userId, skillName);
        int skillRank = skillInfo.Rank;
        bool isTagSkill = skillInfo.IsTag;
        string attributeUsed = attributeName.ToUpper();
        int attributeValue;

        if (useLuck)
        {
            var (success, remainingPS) = await _dbService.SpendLuckPointsAsync(guildId, userId, 1);
            if (!success) { return (null, $"❌ Você tentou usar 'Cartas Marcadas', mas não tem Pontos de Sorte! Você tem {remainingPS} PS.", null); }
            attributeValue = characterStats!["SOR"];
            attributeUsed = $"SOR (Cartas Marcadas)";
            luckSpendMessage = $"⚡ 1 PS gasto! Restam {remainingPS}.";
        }
        else
        {
            attributeValue = characterStats![attributeName.ToUpper()];
        }

        int numDice = 2;
        if (diceToBuy > 0)
        {
            var (success, remainingAP) = await _dbService.SpendActionPointsAsync(guildId, costInAP);
            if (!success) { return (null, $"❌ Você tentou comprar {diceToBuy}d20 (custo {costInAP} PA), mas o grupo só tem {remainingAP} PA!", null); }
            numDice += diceToBuy;
            apSpendMessage = $"💸 {costInAP} PA gastos! Restam {remainingAP} PA no grupo.";
        }

        int targetNumber = attributeValue + skillRank;
        int tagSkillRank = isTagSkill ? skillRank : 0;

        List<int> rolls = new List<int>();
        for (int i = 0; i < numDice; i++) { rolls.Add(_random.Next(1, 21)); }

        var embed = await BuildTestResultEmbed(guildId, username, skillName, attributeUsed, attributeValue, skillRank, difficulty, isTagSkill, targetNumber, tagSkillRank, rolls, luckSpendMessage, apSpendMessage, 0);

        var cache = new UserLastRoll
        {
            GuildId = guildId,
            Rolls = rolls,
            TargetNumber = targetNumber,
            Difficulty = difficulty,
            TagSkillRank = tagSkillRank,
            SkillName = skillName,
            AttributeUsed = attributeUsed,
            AttributeValue = attributeValue,
            SkillRank = skillRank,
            IsTagSkill = isTagSkill,
            LuckSpendMessage = luckSpendMessage,
            ApSpendMessage = apSpendMessage
        };

        return (embed, null, cache);
    }


    public async Task HandleDamageCommandAsync(SocketMessage message)
    {
        string content = message.Content;
        int extraDice = 0;
        var extraRegex = @"--extra\s+(\d+)";
        var extraMatch = Regex.Match(content, extraRegex);
        if (extraMatch.Success)
        {
            extraDice = int.Parse(extraMatch.Groups[1].Value);
            content = Regex.Replace(content, extraRegex, "").Trim();
        }
        var matches = Regex.Matches(content, @"\d+");
        if (matches.Count < 1) { await message.Channel.SendMessageAsync("Formato inválido. Use: `!dano [NumeroDeDados]`\nOpcional: `--extra [dados_bonus]`"); return; }
        int baseDice = int.Parse(matches[0].Value);
        int totalDice = baseDice + extraDice;
        if (totalDice < 1 || totalDice > 50) { await message.Channel.SendMessageAsync("Número de dados inválido. (Total de 1-50)"); return; }

        List<int> rolls = new List<int>();
        int totalDamage = 0;
        int totalEffects = 0;
        for (int i = 0; i < totalDice; i++) { rolls.Add(_random.Next(1, 7)); }
        StringBuilder rollDetails = new StringBuilder();
        foreach (int roll in rolls)
        {
            switch (roll)
            {
                case 1: totalDamage += 1; rollDetails.Append("` 1 ` "); break;
                case 2: totalDamage += 2; rollDetails.Append("` 2 ` "); break;
                case 3: case 4: rollDetails.Append("` - ` "); break;
                case 5: case 6: totalDamage += 1; totalEffects += 1; rollDetails.Append("` 1+Efeito ` "); break;
            }
        }
        string description = $"Rolando **{totalDice}** Dado(s) de Combate...";
        if (extraDice > 0) { description = $"Rolando **{baseDice}** (base) + **{extraDice}** (extra) = **{totalDice}** Dados de Combate..."; }
        var embedBuilder = new EmbedBuilder()
            .WithTitle("Rolagem de Dano de Combate")
            .WithDescription(description)
            .AddField("Resultados Individuais", rollDetails.ToString())
            .AddField("Dano Total", $"💥 **{totalDamage}** Dano(s)")
            .AddField("Efeitos Ativados", $"⚛️ **{totalEffects}** Efeito(s)")
            .WithColor(Color.Orange)
            .WithFooter($"Pedido por {message.Author.Username}");
        await message.Channel.SendMessageAsync(embed: embedBuilder.Build());
    }

    public async Task HandleRerollCommandAsync(SocketUserMessage message, ulong guildId, ulong userId)
    {
        if (!_lastRollCache.TryGetValue(userId, out var lastRoll)) { await message.Channel.SendMessageAsync("Você não tem uma rolagem recente para rerrolar. Use `!teste` primeiro."); return; }
        string[] parts = message.Content.Split(' ');
        if (parts.Length < 2 || !int.TryParse(parts[1], out int dieToReroll)) { await message.Channel.SendMessageAsync("Formato inválido. Use: `!rerrolar [dado]` (ex: `!rerrolar 19`)."); return; }
        if (!lastRoll.Rolls.Contains(dieToReroll)) { await message.Channel.SendMessageAsync($"Sua última rolagem (`{string.Join(", ", lastRoll.Rolls)}`) não contém um `{dieToReroll}`."); return; }

        var (spendSuccess, remainingPS) = await _dbService.SpendLuckPointsAsync(guildId, userId, 1);
        if (!spendSuccess) { await message.Channel.SendMessageAsync($"❌ Você tentou usar 'Sorte Grande', mas não tem Pontos de Sorte! Você tem {remainingPS} PS."); return; }
        string luckMessage = $"🍀 1 PS gasto (Sorte Grande)! Restam {remainingPS}.";

        lastRoll.Rolls.Remove(dieToReroll);
        int newRoll = _random.Next(1, 21);
        lastRoll.Rolls.Add(newRoll);

        string combinedLuckMessage = string.IsNullOrEmpty(lastRoll.LuckSpendMessage) ? luckMessage : $"{lastRoll.LuckSpendMessage}\n{luckMessage}";
        var newEmbed = await BuildTestResultEmbed(
            lastRoll.GuildId, message.Author.Username, lastRoll.SkillName, lastRoll.AttributeUsed, lastRoll.AttributeValue,
            lastRoll.SkillRank, lastRoll.Difficulty, lastRoll.IsTagSkill, lastRoll.TargetNumber,
            lastRoll.TagSkillRank, lastRoll.Rolls, combinedLuckMessage, lastRoll.ApSpendMessage, 1);

        await lastRoll.OriginalMessage.ModifyAsync(msg => msg.Embed = newEmbed);
        _lastRollCache.Remove(userId);
        try { await message.DeleteAsync(); } catch { /* Ignora */ }
    }

    public async Task HandleCraftCommandAsync(SocketUserMessage message, ulong guildId, ulong userId)
    {
        string[] parts = message.Content.Split(' ');
        if (parts.Length < 2)
        {
            await message.Channel.SendMessageAsync("Formato inválido. Use: `!fabricar [nome do item ou mod]`");
            return;
        }
        string itemName = string.Join(" ", parts.Skip(1)).Trim().Replace("\"", "");

        var recipe = await _dbService.GetRecipeAsync(itemName);
        if (recipe == null)
        {
            await message.Channel.SendMessageAsync($"Não encontrei uma receita de fabricação para: `{itemName}`.");
            return;
        }

        var characterStats = await _dbService.GetCharacterAsync(guildId, userId);
        if (characterStats == null)
        {
            await message.Channel.SendMessageAsync("Você não tem um personagem registrado. Use `!registrar` ou `!criar-personagem` primeiro.");
            return;
        }
        var skillInfo = await _dbService.GetCharacterSkillAsync(guildId, userId, recipe.Pericia.ToLower());

        int intRank = characterStats["INT"];
        int skillRank = skillInfo.Rank;
        bool isTagSkill = skillInfo.IsTag;

        int difficulty = Math.Max(0, recipe.Complexidade - skillRank);
        int targetNumber = intRank + skillRank;
        int tagSkillRank = isTagSkill ? skillRank : 0;

        List<int> rolls = new List<int> { _random.Next(1, 21), _random.Next(1, 21) };
        int successes = 0;
        int complications = 0;
        foreach (int roll in rolls)
        {
            if (roll <= targetNumber)
            {
                successes++;
                if (roll == 1 || (tagSkillRank > 0 && roll <= tagSkillRank)) { successes++; }
            }
            if (roll == 20) { complications++; }
        }
        bool isSuccess = successes >= difficulty;

        string resultMessage;
        string footerMessage;
        Color resultColor;

        if (isSuccess)
        {
            resultMessage = "✅ SUCESSO";
            resultColor = Color.Green;
            footerMessage = "Item fabricado! Os materiais foram consumidos.";
        }
        else
        {
            resultMessage = "❌ FALHA";
            resultColor = Color.Red;

            if (complications > 0)
            {
                footerMessage = "Falha! Os materiais foram desperdiçados devido a uma complicação.";
            }
            else if (recipe.Pericia == "Ciências" || recipe.Pericia == "Sobrevivência" || recipe.Pericia == "Explosivos")
            {
                footerMessage = "Falha! Os ingredientes foram perdidos.";
            }
            else
            {
                footerMessage = "Falha! Os materiais não foram consumidos.";
            }
        }

        var embed = new EmbedBuilder()
            .WithTitle($"🛠️ Tentativa de Fabricação: {recipe.ItemName}")
            .WithColor(resultColor)
            .AddField("Receita", $"`{recipe.ItemName}` (Complexidade: {recipe.Complexidade}, Perícia: {recipe.Pericia})")
            .AddField("Seu Teste", $"INT ({intRank}) + {recipe.Pericia} ({skillRank}) = **Alvo {targetNumber}**")
            .AddField("Dificuldade do Teste", $"{recipe.Complexidade} (Complexidade) - {skillRank} (Rank) = **Dificuldade {difficulty}**")
            .AddField("Rolagem (2d20)", $"` {string.Join(", ", rolls)} `")
            .AddField("Resultado", $"**{resultMessage}** ({successes} Sucesso(s))")
            .AddField("Materiais Necessários", GetMaterialsForRecipe(recipe))
            .WithFooter(footerMessage);

        if (complications > 0)
        {
            embed.AddField("Complicações", $"**{complications}** Complicação(ões)!");
        }

        await message.Channel.SendMessageAsync(embed: embed.Build());
    }

    public async Task HandleActionPointCommandAsync(SocketMessage message, ulong guildId)
    {
        string[] parts = message.Content.Split(' ');
        string command = parts.Length > 1 ? parts[1].ToLower() : "ver";
        int value = 0;
        if (parts.Length > 2 && int.TryParse(parts[2], out int parsedValue)) { value = parsedValue; }
        int currentPA;
        switch (command)
        {
            case "adicionar":
            case "add":
                currentPA = await _dbService.AddActionPointsAsync(guildId, value);
                await message.Channel.SendMessageAsync($"PA adicionados. Total do grupo: **{currentPA} / {MAX_ACTION_POINTS}**");
                break;
            case "gastar":
            case "usar":
                var (success, remainingPA) = await _dbService.SpendActionPointsAsync(guildId, value);
                if (!success) { await message.Channel.SendMessageAsync($"Não há PAs suficientes! O grupo só tem **{remainingPA}**."); }
                else { await message.Channel.SendMessageAsync($"PAs gastos. Total do grupo: **{remainingPA} / {MAX_ACTION_POINTS}**"); }
                break;
            case "definir":
            case "set":
                await _dbService.SetActionPointsAsync(guildId, value);
                currentPA = Math.Clamp(value, 0, MAX_ACTION_POINTS);
                await message.Channel.SendMessageAsync($"PAs definidos. Total do grupo: **{currentPA} / {MAX_ACTION_POINTS}**");
                break;
            case "zerar":
                await _dbService.SetActionPointsAsync(guildId, 0);
                await message.Channel.SendMessageAsync($"PAs zerados. Total do grupo: **0 / {MAX_ACTION_POINTS}**");
                break;
            case "ver":
            case "status":
            default:
                currentPA = await _dbService.GetActionPointsAsync(guildId);
                await message.Channel.SendMessageAsync($"O grupo tem **{currentPA} / {MAX_ACTION_POINTS}** Pontos de Ação.");
                break;
        }
    }

    public async Task HandleLuckPointCommandAsync(SocketMessage message, ulong guildId, ulong userId)
    {
        string[] parts = message.Content.Split(' ');
        string command = parts.Length > 1 ? parts[1].ToLower() : "ver";
        int value = 0;
        if (parts.Length > 2 && int.TryParse(parts[2], out int parsedValue)) { value = parsedValue; }
        var (currentPS, maxPS) = await _dbService.GetLuckPointsAsync(guildId, userId);
        if (maxPS == 0) { await message.Channel.SendMessageAsync("Você não tem um personagem registrado. Use `!registrar` ou `!criar-personagem` primeiro."); return; }

        switch (command)
        {
            case "gastar":
            case "usar":
                value = (value == 0) ? 1 : value;
                var (success, remainingPS) = await _dbService.SpendLuckPointsAsync(guildId, userId, value);
                if (!success) { await message.Channel.SendMessageAsync($"Não há Pontos de Sorte suficientes! Você só tem **{remainingPS}**."); }
                else { await message.Channel.SendMessageAsync($"PS gasto. Você tem **{remainingPS} / {maxPS}** Pontos de Sorte restantes."); }
                break;
            case "redefinir":
            case "reset":
                currentPS = await _dbService.SetLuckPointsAsync(guildId, userId, maxPS);
                await message.Channel.SendMessageAsync($"Pontos de Sorte redefinidos. Você tem **{currentPS} / {maxPS}** PS.");
                break;
            case "definir":
            case "set":
                currentPS = await _dbService.SetLuckPointsAsync(guildId, userId, value);
                await message.Channel.SendMessageAsync($"Pontos de Sorte definidos. Você tem **{currentPS} / {maxPS}** PS.");
                break;
            case "ver":
            case "status":
            default:
                await message.Channel.SendMessageAsync($"Você tem **{currentPS} / {maxPS}** Pontos de Sorte.");
                break;
        }
    }

    public async Task HandleXPCommandAsync(SocketMessage message, ulong guildId)
    {
        string[] parts = message.Content.Split(' ');
        if (parts.Length < 3)
        {
            await message.Channel.SendMessageAsync("Formato inválido. Use: `!xp add [valor]` OU `!xp derrota [nome_npc]`");
            return;
        }

        string command = parts[1].ToLower();
        int xpAmount = 0;
        string xpSource = string.Join(" ", parts.Skip(2));

        if (command == "add" && int.TryParse(xpSource, out int manualXP))
        {
            xpAmount = Math.Max(0, manualXP);
            xpSource = $"XP manual ({xpAmount})";
        }
        else if (command == "derrota")
        {
            var npcStats = await _dbService.GetCreatureStatsAsync(xpSource);
            if (npcStats == null)
            {
                await message.Channel.SendMessageAsync($"NPC/Criatura '{xpSource}' não encontrado para calcular o XP de derrota.");
                return;
            }
            xpAmount = await _dbService.GetXPAmountForNPC(npcStats);
            xpSource = $"Derrota de {npcStats.Nome} (Nível {npcStats.Nivel}, {xpAmount} XP)";
        }
        else
        {
            await message.Channel.SendMessageAsync("Comando inválido. Use: `!xp add [valor]` OU `!xp derrota [nome_npc]`");
            return;
        }

        if (xpAmount <= 0)
        {
            await message.Channel.SendMessageAsync($"A fonte de XP '{xpSource}' concede 0 XP.");
            return;
        }

        // Aplicar ao autor da mensagem (Correção do Bug Anterior)
        var allCharacters = await _dbService.GetAllCharactersInGuildAsync(guildId);
        var authorData = allCharacters.FirstOrDefault(c => c.UserId == message.Author.Id);

        if (authorData == default)
        {
            await message.Channel.SendMessageAsync($"Você ({message.Author.Username}) não tem um personagem registrado para receber XP.");
            return;
        }

        var (newXP, newLevel, xpToNextLevel) = await _dbService.AddXPToCharacter(guildId, authorData.UserId, xpAmount);

        var embed = new EmbedBuilder()
            .WithTitle($"⭐ XP Distribuído: +{xpAmount} XP por {xpSource}")
            .WithColor(Color.Gold);

        StringBuilder summary = new StringBuilder();

        if (newLevel > authorData.Level)
        {
            summary.AppendLine($"**🎉 {authorData.Name} subiu para o Nível {newLevel}!**");
        }
        summary.AppendLine($"`{authorData.Name}`: {newXP} XP (Próximo nível em {xpToNextLevel} XP)");

        embed.WithDescription(summary.ToString());
        await message.Channel.SendMessageAsync(embed: embed.Build());
    }

    private string GetMaterialsForRecipe(Recipe recipe)
    {
        if (recipe.Materiais != null)
        {
            return recipe.Materiais.Replace("x", "×");
        }

        switch (recipe.Complexidade)
        {
            case 1: return "Materiais Comuns ×2";
            case 2: return "Materiais Comuns ×3";
            case 3: return "Materiais Comuns ×4, Materiais Incomuns ×2";
            case 4: return "Materiais Comuns ×5, Materiais Incomuns ×3";
            case 5: return "Materiais Comuns ×6, Materiais Incomuns ×4, Materiais Raros ×2";
            case 6: return "Materiais Comuns ×7, Materiais Incomuns ×5, Materiais Raros ×3";
            case 7: return "Materiais Comuns ×8, Materiais Incomuns ×6, Materiais Raros ×4";
            default: return "Materiais não especificados";
        }
    }


    #endregion

    #region Classes Auxiliares e Builders (GameService)


    public class UserLastRoll
    {
        public IUserMessage OriginalMessage { get; set; } = null!;
        public ulong GuildId { get; set; }
        public List<int> Rolls { get; set; } = new List<int>();
        public int TargetNumber { get; set; }
        public int Difficulty { get; set; }
        public int TagSkillRank { get; set; }
        public string SkillName { get; set; } = "";
        public string AttributeUsed { get; set; } = "";
        public int AttributeValue { get; set; }
        public int SkillRank { get; set; }
        public bool IsTagSkill { get; set; }
        public string LuckSpendMessage { get; set; } = "";
        public string ApSpendMessage { get; set; } = "";
    }


    public async Task<Embed> BuildTestResultEmbed(ulong guildId, string username, string skillName, string attributeUsed, int attributeValue,
        int skillRank, int difficulty, bool isTagSkill, int targetNumber, int tagSkillRank,
        List<int> rolls, string luckSpendMessage, string apSpendMessage, int rerollCount)
    {
        int successes = 0;
        int complications = 0;
        foreach (int roll in rolls)
        {
            if (roll <= targetNumber)
            {
                successes++;
                if (roll == 1 || (tagSkillRank > 0 && roll <= tagSkillRank)) { successes++; }
            }
            if (roll == 20) { complications++; }
        }
        bool isSuccess = successes >= difficulty;
        int actionPointsGained = isSuccess ? (successes - difficulty) : 0;
        int currentTotalPA = 0;

        // CORREÇÃO: Apenas adiciona PA ao DB se for a rolagem inicial (rerollCount == 0)
        if (actionPointsGained > 0 && rerollCount == 0)
        {
            currentTotalPA = await _dbService.AddActionPointsAsync(guildId, actionPointsGained);
        }
        else
        {
            currentTotalPA = await _dbService.GetActionPointsAsync(guildId);
        }

        string title = rerollCount > 0 ? $"RERROLAGEM de {skillName.ToUpper()} (Alvo: {targetNumber})" : $"Teste de {skillName.ToUpper()} (Alvo: {targetNumber})";
        string embedDescription = $"**Personagem:** {username}\n" +
                                  $"**Atributo ({attributeUsed}):** {attributeValue} | **Rank ({skillName}):** {skillRank} | **Dificuldade:** {difficulty}" +
                                  (isTagSkill ? " (Perícia Marcada!)" : "");
        if (!string.IsNullOrEmpty(luckSpendMessage)) { embedDescription += $"\n{luckSpendMessage}"; }
        if (!string.IsNullOrEmpty(apSpendMessage)) { embedDescription += $"\n{apSpendMessage}"; }

        var embedBuilder = new EmbedBuilder()
            .WithTitle(title)
            .WithDescription(embedDescription)
            .AddField($"Rolagens ({rolls.Count}d20)", $"` {string.Join(", ", rolls)} `")
            .AddField("Resultado", $"**{successes} Sucesso(s)**")
            .WithFooter($"Pedido por {username}");

        if (isSuccess)
        {
            embedBuilder.AddField("Status", "✅ SUCESSO").WithColor(Color.Green);
            if (actionPointsGained > 0)
            {
                embedBuilder.AddField("Pontos de Ação Gerados", $"+{actionPointsGained} PA");
                if (rerollCount == 0) { embedBuilder.AddField("Total de PA do Grupo", $"**{currentTotalPA} / {MAX_ACTION_POINTS}**"); }
            }
        }
        else { embedBuilder.AddField("Status", "❌ FALHA").WithColor(Color.Red); }
        if (complications > 0) { embedBuilder.AddField("Complicações", $"**{complications}** Complicação(ões)!"); }

        return embedBuilder.Build();
    }

    /// <summary>
    /// Salva publicamente a última rolagem de um usuário no cache.
    /// </summary>
    public void CacheLastRoll(ulong userId, UserLastRoll rollData)
    {
        if (rollData == null) return;
        _lastRollCache[userId] = rollData;
    }

    #endregion
}