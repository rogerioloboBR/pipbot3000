using Discord;
using Discord.WebSocket;
using PipBot3000;
using System.Text;
using System.Text.RegularExpressions;



public class CharacterService
{
    private readonly DatabaseService _dbService;
    private readonly Rulebook _rulebook;
    private readonly Dictionary<ulong, CharacterCreationState> _creationCache = new Dictionary<ulong, CharacterCreationState>();
    private readonly Dictionary<ulong, GMContentCreationState> _gmCreationCache = new Dictionary<ulong, GMContentCreationState>();


    public CharacterService(DatabaseService dbService, Rulebook rulebook)
    {
        _dbService = dbService;
        _rulebook = rulebook;
    }

    // --- MÉTODOS DE ROTEAMENTO DM (Principal) ---
    public async Task HandleDmCreationStepAsync(SocketUserMessage message)
    {
        ulong userId = message.Author.Id;

        if (message.Author.IsBot) return;

        if (_creationCache.ContainsKey(userId))
        {
            await ProcessDmPlayerCreationStepAsync(message);
        }
        else if (_gmCreationCache.ContainsKey(userId))
        {
            await ProcessDmGMCreateStepAsync(message);
        }
    }

    // --- LÓGICA DO ASSISTENTE PJ ---

    public async Task HandleStartCreationCommandAsync(SocketUserMessage message, SocketGuild guild)
    {
        ulong userId = message.Author.Id;

        if (_creationCache.ContainsKey(userId))
        {
            await message.Channel.SendMessageAsync($"{message.Author.Mention}, você já está no meio da criação de um personagem. Por favor, verifique suas Mensagens Diretas (DMs).");
            return;
        }

        var newState = new CharacterCreationState
        {
            GuildId = guild.Id,
            GuildName = guild.Name,
            CurrentStep = CreationStep.Origin
        };
        _creationCache[userId] = newState;

        await message.Channel.SendMessageAsync($"{message.Author.Mention}, enviei uma Mensagem Direta (DM) para você para começarmos a criação do seu personagem!");

        try
        {
            var dmChannel = await message.Author.CreateDMChannelAsync();
            await dmChannel.SendMessageAsync(
                $"Olá! Vamos começar a criar seu personagem para o servidor **{guild.Name}**.\n" +
                "*(Você pode digitar `cancelar` a qualquer momento para parar)*.\n\n" +
                "**Etapa 1: Origem** (pág. 51)\n" +
                "Por favor, escolha sua Origem (responda apenas com o número):\n" +
                "`1.` Habitante do Refúgio\n" +
                "`2.` Necrótico\n" +
                "`3.` Supermutante\n" +
                "`4.` Iniciado da Irmandade\n" +
                "`5.` Sobrevivente\n" +
                "`6.` Mr. Handy (Origem Avançada)"
            );
        }
        catch (Exception)
        {
            await message.Channel.SendMessageAsync($"{message.Author.Mention}, eu não consegui te enviar uma DM. Por favor, habilite as DMs deste servidor para continuar.");
            _creationCache.Remove(userId);
        }
    }

    private async Task ProcessDmPlayerCreationStepAsync(SocketUserMessage message)
    {
        ulong userId = message.Author.Id;
        if (!_creationCache.TryGetValue(userId, out var state))
        {
            return;
        }

        string response = message.Content.ToLower().Trim();

        if (response == "cancelar")
        {
            _creationCache.Remove(userId);
            await message.Channel.SendMessageAsync("Criação de personagem cancelada.");
            return;
        }

        try
        {
            switch (state.CurrentStep)
            {
                case CreationStep.Origin:
                    await ProcessStep_Origin(message, state, response);
                    break;
                case CreationStep.Special:
                    await ProcessStep_Special(message, state, response);
                    break;
                case CreationStep.TagSkills:
                    await ProcessStep_TagSkills(message, state, response);
                    break;
                case CreationStep.DistributeSkills:
                    await ProcessStep_DistributeSkills(message, state, response);
                    break;
                case CreationStep.Name:
                    await ProcessStep_Name(message, state, response);
                    break;
            }
        }
        catch (Exception ex)
        {
            await message.Channel.SendMessageAsync($"Ocorreu um erro inesperado: {ex.Message}. A criação foi cancelada. Por favor, tente `!criar-personagem` no servidor novamente.");
            _creationCache.Remove(userId);
        }
    }

    // --- MÉTODOS ProcessStep_PJ ---
    private async Task ProcessStep_Origin(SocketUserMessage message, CharacterCreationState state, string response)
    {
        var origins = new[] { "Habitante do Refúgio", "Necrótico", "Supermutante", "Iniciado da Irmandade", "Sobrevivente", "Mr. Handy" };
        if (int.TryParse(response, out int choice) && choice >= 1 && choice <= origins.Length)
        {
            state.Origin = origins[choice - 1];
            state.CurrentStep = CreationStep.Special;

            await message.Channel.SendMessageAsync(
                $"Ótimo: **{state.Origin}**. \n\n" +
                "**Etapa 2: S.P.E.C.I.A.L.** (pág. 58)\n" +
                "Você começa com 5 em todos os atributos e tem **5 pontos** para distribuir (Total de 40).\n" +
                "*Nota: Nenhum atributo pode ser menor que 4 ou maior que 10.*\n\n" +
                "Por favor, digite seus 7 atributos na ordem (FOR PER RES CAR INT AGI SOR), separados por espaço.\n" +
                "Exemplo: `6 7 5 6 8 5 4`"
            );
        }
        else
        {
            await message.Channel.SendMessageAsync("Resposta inválida. Por favor, digite apenas o número (de 1 a 6).");
        }
    }

    private async Task ProcessStep_Special(SocketUserMessage message, CharacterCreationState state, string response)
    {
        var matches = Regex.Matches(response, @"\d+");
        if (matches.Count != 7)
        {
            await message.Channel.SendMessageAsync("Formato inválido. Você deve fornecer exatamente 7 números (FOR PER RES CAR INT AGI SOR). Tente novamente.");
            return;
        }

        var points = matches.Select(m => int.Parse(m.Value)).ToArray();

        if (points.Sum() != 40)
        {
            await message.Channel.SendMessageAsync($"Soma inválida. Seus atributos somam {points.Sum()}, mas deveriam somar 40 (7x5 + 5 pontos). Tente novamente.");
            return;
        }
        if (points.Any(p => p < 4 || p > 10))
        {
            await message.Channel.SendMessageAsync("Atributo inválido. Nenhum atributo pode ser menor que 4 ou maior que 10. Tente novamente.");
            return;
        }

        state.Special = points;
        state.CurrentStep = CreationStep.TagSkills;

        await message.Channel.SendMessageAsync(
            $"S.P.E.C.I.A.L. definido: **FOR:** {points[0]} | **PER:** {points[1]} | **RES:** {points[2]} | **CAR:** {points[3]} | **INT:** {points[4]} | **AGI:** {points[5]} | **SOR:** {points[6]}\n\n" +
            "**Etapa 3: Perícias Marcadas** (pág. 58)\n" +
            "Escolha suas **3 Perícias Marcadas** (Tag Skills). Elas começarão automaticamente no Rank 2.\n" +
            "Por favor, digite 3 perícias (use os nomes sem espaço, ex: `armaspequenas`) separadas por vírgula.\n" +
            "Exemplo: `armaspequenas, reparo, ciencias`"
        );
    }

    private async Task ProcessStep_TagSkills(SocketUserMessage message, CharacterCreationState state, string response)
    {
        var skills = response.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                             .Select(s => s.ToLower())
                             .ToList();

        if (skills.Count != 3)
        {
            await message.Channel.SendMessageAsync("Formato inválido. Você deve fornecer exatamente 3 perícias. Tente novamente.");
            return;
        }

        foreach (var skill in skills)
        {
            if (!_rulebook.SkillToAttribute.ContainsKey(skill))
            {
                await message.Channel.SendMessageAsync($"Perícia inválida: `{skill}`. Use nomes sem espaço (ex: `armaspequenas`). Tente novamente.");
                return;
            }
        }

        state.TagSkills = skills;
        state.CurrentStep = CreationStep.DistributeSkills;

        foreach (var kvp in _rulebook.SkillToAttribute)
        {
            state.Skills.Add(kvp.Key, 0);
        }

        foreach (var tagSkill in state.TagSkills)
        {
            state.Skills[tagSkill] = 2;
        }

        int totalPoints = 9 + state.Special[4];
        string skillList = string.Join(", ", _rulebook.SkillToAttribute.Keys.OrderBy(k => k));


        await message.Channel.SendMessageAsync(
            $"Perícias Marcadas definidas: **{string.Join(", ", skills)}** (Rank 2 aplicado).\n\n" +
            "**Etapa 4: Distribuir Pontos de Perícia** (pág. 58)\n" +
            $"Você tem **{totalPoints} pontos** para distribuir entre suas perícias (Rank Máximo 3 nesta etapa).\n" +
            "Digite uma lista de comandos `[pericia] [rank]` separados por vírgula.\n" +
            "*Você não precisa listar as Perícias Marcadas novamente.*\n\n" +
            "**Exemplo:** `medicina 1, furtividade 3, pilotagem 2`\n\n" +
            $"**Lista de Perícias:** {skillList}\n" +
            $"**Total de Pontos:** {totalPoints}"
        );
    }

    private async Task ProcessStep_DistributeSkills(SocketUserMessage message, CharacterCreationState state, string response)
    {
        var rawCommands = response.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        var pointsToApply = new Dictionary<string, int>();
        int totalPointsSpent = 0;
        int initialPoints = 9 + state.Special[4];

        foreach (var rawCommand in rawCommands)
        {
            var parts = rawCommand.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !int.TryParse(parts[1], out int rank) || rank < 0 || rank > 3)
            {
                await message.Channel.SendMessageAsync($"Erro: Comando inválido `{rawCommand.Trim()}`. Use o formato `[pericia] [rank]` (rank entre 0 e 3).");
                return;
            }

            string skillName = parts[0].ToLower();

            if (!state.Skills.ContainsKey(skillName))
            {
                await message.Channel.SendMessageAsync($"Erro: Perícia desconhecida `{skillName}`. Tente novamente.");
                return;
            }

            if (state.TagSkills.Contains(skillName) && rank < 2)
            {
                await message.Channel.SendMessageAsync($"Erro: Perícia Marcada `{skillName}` deve ter Rank mínimo 2.");
                return;
            }

            if (rank > 3)
            {
                await message.Channel.SendMessageAsync($"Erro: Rank máximo permitido nesta etapa é 3. Você tentou Rank {rank} para {skillName}.");
                return;
            }

            int cost = state.TagSkills.Contains(skillName) ? Math.Max(0, rank - 2) : rank;
            totalPointsSpent += cost;
            pointsToApply[skillName] = rank;
        }

        if (totalPointsSpent > initialPoints)
        {
            await message.Channel.SendMessageAsync($"Erro: Você tentou gastar {totalPointsSpent} pontos, mas só tem {initialPoints} disponíveis. Tente novamente.");
            return;
        }

        foreach (var kvp in pointsToApply)
        {
            state.Skills[kvp.Key] = kvp.Value;
        }


        state.CurrentStep = CreationStep.Name;

        var summary = new StringBuilder();
        summary.AppendLine("Pontos de Perícia Finalizados! Resumo:");
        foreach (var kvp in state.Skills.OrderByDescending(s => s.Value))
        {
            if (kvp.Value > 0)
            {
                summary.AppendLine($"- **{kvp.Key.ToUpper()}**: {kvp.Value} {(state.TagSkills.Contains(kvp.Key) ? " (MARCADA)" : "")}");
            }
        }
        summary.AppendLine($"\n**Pontos Usados:** {totalPointsSpent} / {initialPoints}");
        summary.AppendLine($"**Pontos Restantes:** {initialPoints - totalPointsSpent}");


        await message.Channel.SendMessageAsync(summary.ToString());
        await message.Channel.SendMessageAsync(
            "**Etapa Final: Nome**\n" +
            "Qual é o nome do seu personagem?"
        );
    }


    private async Task ProcessStep_Name(SocketUserMessage message, CharacterCreationState state, string response)
    {
        state.Name = response.Trim();
        state.CurrentStep = CreationStep.Done;

        await message.Channel.SendMessageAsync($"Registrando personagem **{state.Name}** no servidor **{state.GuildName}**...");

        var s = state.Special;
        await _dbService.CreateCharacterAsync(state.GuildId, message.Author.Id, state.Name, s[0], s[1], s[2], s[3], s[4], s[5], s[6]);

        var saveTasks = new List<Task>();
        foreach (var kvp in state.Skills)
        {
            if (kvp.Value > 0)
            {
                bool isTag = state.TagSkills.Contains(kvp.Key);
                saveTasks.Add(_dbService.SetCharacterSkillAsync(state.GuildId, message.Author.Id, kvp.Key, kvp.Value, isTag));
            }
        }
        await Task.WhenAll(saveTasks);

        int pv = s[2] + s[6];
        int defesa = s[5] >= 9 ? 2 : 1;
        int iniciativa = s[1] + s[5];

        await message.Channel.SendMessageAsync(
            "**🎉 PERSONAGEM CRIADO E REGISTRADO!**\n\n" +
            $"**Nome:** {state.Name} | **Origem:** {state.Origin}\n" +
            $"**S.P.E.C.I.A.L.:** FOR {s[0]} PER {s[1]} RES {s[2]} CAR {s[3]} INT {s[4]} AGI {s[5]} SOR {s[6]}\n\n" +
            $"**PV:** {pv}\n" +
            $"**Defesa:** {defesa}\n" +
            $"**Iniciativa:** {iniciativa}\n" +
            $"**Pontos de Sorte:** {s[6]} / {s[6]}\n\n" +
            "*Sua ficha está pronta para ser usada com `!teste` e outros comandos!*"
        );

        _creationCache.Remove(message.Author.Id);
    }

    // --- MÉTODOS PJ AUXILIARES (COMPLETOS) ---
    public async Task HandleRegisterCommandAsync(SocketUserMessage message, ulong guildId, ulong userId)
    {
        string argsString = message.Content.Substring("!registrar".Length).Trim();
        string name = "";

        if (argsString.StartsWith("\""))
        {
            int endIndex = argsString.IndexOf('\"', 1);
            if (endIndex == -1) { await message.Channel.SendMessageAsync("Formato inválido. Se o nome tiver espaços, use aspas: `!registrar \"Meu Nome\" 5 5 5 5 5 5 5`"); return; }
            name = argsString.Substring(1, endIndex - 1);
            argsString = argsString.Substring(endIndex + 1).Trim();
        }
        else
        {
            var firstSpace = argsString.IndexOf(' ');
            if (firstSpace == -1) { await message.Channel.SendMessageAsync("Formato inválido. Faltam os atributos S.P.E.C.I.A.L."); return; }
            name = argsString.Substring(0, firstSpace);
            argsString = argsString.Substring(firstSpace).Trim();
        }

        var matches = Regex.Matches(argsString, @"\d+");
        if (matches.Count != 7) { await message.Channel.SendMessageAsync("Formato inválido. Você deve fornecer 7 números para S.P.E.C.I.A.L. (FOR PER RES CAR INT AGI SOR)."); return; }

        try
        {
            int s = int.Parse(matches[0].Value); int p = int.Parse(matches[1].Value); int e = int.Parse(matches[2].Value);
            int c = int.Parse(matches[3].Value); int i = int.Parse(matches[4].Value); int a = int.Parse(matches[5].Value);
            int l = int.Parse(matches[6].Value);

            int sum = s + p + e + c + i + a + l;
            if (sum != 40)
            {
                await message.Channel.SendMessageAsync($"Soma de S.P.E.C.I.A.L. inválida. A soma deve ser 40 (7x5 + 5 pontos extras). Sua soma foi: {sum}.");
                return;
            }

            await _dbService.CreateCharacterAsync(guildId, userId, name, s, p, e, c, i, a, l);
            await message.Channel.SendMessageAsync($"Personagem **{name}** registrado com sucesso. Sua ficha inicial está pronta.");
        }
        catch (Exception ex)
        {
            await message.Channel.SendMessageAsync($"Ocorreu um erro ao registrar: {ex.Message}");
        }
    }

    public async Task HandleSkillCommandAsync(SocketUserMessage message, ulong guildId, ulong userId)
    {
        string[] parts = message.Content.Split(' ');
        if (parts.Length < 3 || !int.TryParse(parts[2], out int rank))
        {
            await message.Channel.SendMessageAsync("Formato inválido. Use: `!pericia [nome_pericia] [rank] [marcada (opcional)]`");
            return;
        }
        string skillName = parts[1].ToLower();
        bool isTag = parts.Length > 3 && parts[3].ToLower() == "marcada";

        if (!_rulebook.SkillToAttribute.ContainsKey(skillName))
        {
            await message.Channel.SendMessageAsync($"Perícia `{skillName}` desconhecida.");
            return;
        }

        try
        {
            if (await _dbService.GetCharacterAsync(guildId, userId) == null)
            {
                await message.Channel.SendMessageAsync("Você não tem um personagem registrado. Use `!registrar` ou `!criar-personagem` primeiro.");
                return;
            }
            await _dbService.SetCharacterSkillAsync(guildId, userId, skillName, rank, isTag);
            await message.Channel.SendMessageAsync($"Perícia atualizada: **{skillName}** definida para **Rank {rank}**" + (isTag ? " (MARCADA)" : ""));
        }
        catch (Exception ex)
        {
            await message.Channel.SendMessageAsync($"Ocorreu um erro ao atualizar a perícia: {ex.Message}");
        }
    }

    public async Task HandleGMToolkitCommandAsync(SocketMessage message, ulong guildId) { /* O corpo deste método está no InfoService.cs */ }


    // --- LÓGICA DO MODO DE CRIAÇÃO GM (NPC/CRIATURA) ---

    public async Task HandleStartGMCreateCommandAsync(SocketUserMessage message, SocketGuild guild)
    {
        ulong userId = message.Author.Id;

        if (_gmCreationCache.ContainsKey(userId))
        {
            await message.Channel.SendMessageAsync($"{message.Author.Mention}, você já está no meio da criação de conteúdo. Por favor, verifique suas Mensagens Diretas (DMs).");
            return;
        }

        var newState = new GMContentCreationState
        {
            GuildId = guild.Id,
            GuildName = guild.Name,
            CurrentStep = GMCreateStep.Type
        };
        _gmCreationCache[userId] = newState;

        await message.Channel.SendMessageAsync($"GM, iniciei o modo de criação de conteúdo (NPC/Criatura) nas suas Mensagens Diretas (DMs).");

        try
        {
            var dmChannel = await message.Author.CreateDMChannelAsync();
            await dmChannel.SendMessageAsync(
                $"Olá, Mestre! Você está criando conteúdo para **{guild.Name}**.\n" +
                "*(Você pode digitar `gm-cancelar` a qualquer momento para parar)*.\n\n" +
                "**Etapa 1: Tipo de Conteúdo**\n" +
                "O que você deseja criar? (Responda apenas com o número):\n" +
                "`1.` NPC (Personagem Notável, Principal, etc.)\n" +
                "`2.` Criatura (Monstro, Animal Mutante, etc.)"
            );
        }
        catch (Exception)
        {
            await message.Channel.SendMessageAsync($"GM, não consegui te enviar uma DM. Habilite as DMs para o GM Toolkit de criação.");
            _gmCreationCache.Remove(userId);
        }
    }

    private async Task ProcessDmGMCreateStepAsync(SocketUserMessage message)
    {
        ulong userId = message.Author.Id;
        if (!_gmCreationCache.TryGetValue(userId, out var state))
        {
            return;
        }

        string response = message.Content.ToLower().Trim();

        if (response == "gm-cancelar")
        {
            _gmCreationCache.Remove(userId);
            await message.Channel.SendMessageAsync("Modo de Criação de Conteúdo cancelado.");
            return;
        }

        try
        {
            switch (state.CurrentStep)
            {
                case GMCreateStep.Type:
                    await ProcessGMStep_Type(message, state, response);
                    break;
                case GMCreateStep.Name:
                    await ProcessGMStep_Name(message, state, response);
                    break;
                case GMCreateStep.LevelAndType:
                    await ProcessGMStep_LevelAndType(message, state, response);
                    break;
                case GMCreateStep.Attributes:
                    await ProcessGMStep_Attributes(message, state, response);
                    break;
                case GMCreateStep.Keywords:
                    await ProcessGMStep_Keywords(message, state, response);
                    break;
                case GMCreateStep.Finalize:
                    await ProcessGMStep_Finalize(message, state, response);
                    break;
            }
        }
        catch (Exception ex)
        {
            await message.Channel.SendMessageAsync($"Ocorreu um erro no passo {state.CurrentStep}: {ex.Message}. A criação foi cancelada. Por favor, digite `!gm-criar` no servidor novamente.");
            _gmCreationCache.Remove(userId);
        }
    }


    // --- Lógica dos Passos de Criação de Conteúdo do GM ---

    private async Task ProcessGMStep_Type(SocketUserMessage message, GMContentCreationState state, string response)
    {
        if (response == "1" || response.Contains("npc"))
        {
            state.Type = "NPC";
            state.CurrentStep = GMCreateStep.Name;
            await message.Channel.SendMessageAsync(
                "Você escolheu criar um **NPC** (Personagem).\n" +
                "Qual o **Nome** deste NPC?"
            );
        }
        else if (response == "2" || response.Contains("criatura"))
        {
            state.Type = "Criatura";
            state.CurrentStep = GMCreateStep.Name;
            await message.Channel.SendMessageAsync(
                "Você escolheu criar uma **Criatura** (Monstro).\n" +
                "Qual o **Nome** desta Criatura?"
            );
        }
        else
        {
            await message.Channel.SendMessageAsync("Opção inválida. Escolha `1` (NPC) ou `2` (Criatura).");
        }
    }

    private async Task ProcessGMStep_Name(SocketUserMessage message, GMContentCreationState state, string response)
    {
        state.Name = response.Trim();
        state.CurrentStep = GMCreateStep.LevelAndType;

        string typePrompt = state.Type == "Criatura" ?
            "Tipos de Criatura (Cap. Dez): `Normal`, `Poderosa` (x2 XP/PV), `Lendária` (x3 XP/PV)" :
            "Tipos de Personagem (Cap. Dez): `Normal`, `Notável` (x2 XP/PS), `Principal` (x3 XP/PS)";

        await message.Channel.SendMessageAsync(
            $"Nome: **{state.Name}**.\n\n" +
            "**Etapa 2: Nível e Tipo**\n" +
            $"Defina o **Nível** (1-21) e o **Tipo**.\n" +
            $"{typePrompt}\n\n" +
            "Exemplo: `10 Principal`"
        );
    }

    private async Task ProcessGMStep_LevelAndType(SocketUserMessage message, GMContentCreationState state, string response)
    {
        string[] parts = response.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2 || !int.TryParse(parts[0], out int level) || level < 1 || level > 21)
        {
            await message.Channel.SendMessageAsync("Formato inválido. Use: `[Nível] [Tipo]` (Nível entre 1 e 21). Tente novamente.");
            return;
        }

        string type = parts[1].Trim();
        state.Level = level;

        if (state.Type == "NPC")
        {
            if (!new[] { "normal", "notável", "principal" }.Contains(type.ToLower()))
            {
                await message.Channel.SendMessageAsync("Tipo de NPC inválido. Use: `Normal`, `Notável` ou `Principal`.");
                return;
            }
            state.Type = type;

            int baseSum = type.ToLower() switch
            {
                "notável" => 42,
                "principal" => 49,
                _ => 35, // Normal
            };

            int requiredSum = baseSum + (level / 2);

            state.CurrentStep = GMCreateStep.Attributes;

            await message.Channel.SendMessageAsync(
               $"NPC **{state.Name}** (Nvl {level}, Tipo {type}) registrado.\n\n" +
               "**Etapa 3: Atributos S.P.E.C.I.A.L.**\n" +
               $"Para um NPC **{type}** de Nível {level}, a soma dos 7 atributos S.P.E.C.I.A.L. deve ser: **{requiredSum}**.\n" +
               "*Lembre-se: Geralmente entre 4 e 10. Digite os 7 atributos em ordem: (FOR PER RES CAR INT AGI SOR).* \n" +
               "Exemplo: `9 8 7 6 5 4 10`"
            );
        }
        else if (state.Type == "Criatura")
        {
            if (!new[] { "normal", "poderosa", "lendária" }.Contains(type.ToLower()))
            {
                await message.Channel.SendMessageAsync("Tipo de Criatura inválido. Use: `Normal`, `Poderosa` ou `Lendária`.");
                return;
            }
            state.Type = type;

            state.CurrentStep = GMCreateStep.Keywords;

            await message.Channel.SendMessageAsync(
                $"Criatura **{state.Name}** (Nvl {level}, Tipo {type}) registrado.\n\n" +
                "**Etapa 3: Palavras-Chave (Criatura)**\n" +
                "Defina as **Keywords** da Criatura (ex: `Mutante, Animal, Carnívoro, Radiações`). Separe por vírgula. (Obrigatório: 1-3)"
            );
        }
    }

    private async Task ProcessGMStep_Attributes(SocketUserMessage message, GMContentCreationState state, string response)
    {
        var matches = Regex.Matches(response, @"\d+");
        if (matches.Count != 7)
        {
            await message.Channel.SendMessageAsync("Formato inválido. Você deve fornecer exatamente 7 números (FOR PER RES CAR INT AGI SOR). Tente novamente.");
            return;
        }

        var points = matches.Select(m => int.Parse(m.Value)).ToArray();
        int sum = points.Sum();

        int baseSum = state.Type.ToLower() switch
        {
            "notável" => 42,
            "principal" => 49,
            _ => 35, // Normal
        };
        int requiredSum = baseSum + (state.Level / 2);

        if (sum != requiredSum)
        {
            await message.Channel.SendMessageAsync($"Soma inválida. Seus atributos somam {sum}, mas deveriam somar **{requiredSum}** para um NPC {state.Type} de Nível {state.Level}. Tente novamente.");
            return;
        }

        state.Attributes = new Dictionary<string, int>
        {
            { "FOR", points[0] }, { "PER", points[1] }, { "RES", points[2] },
            { "CAR", points[3] }, { "INT", points[4] }, { "AGI", points[5] },
            { "SOR", points[6] }
        };

        state.CurrentStep = GMCreateStep.Keywords;

        await message.Channel.SendMessageAsync(
            $"Atributos S.P.E.C.I.A.L. definidos. Soma {sum}.\n\n" +
            "**Etapa 4: Palavras-Chave (NPC)**\n" +
            "Defina as **Keywords** do NPC (ex: `Humano, Invasor, Membro da Fraternidade`). Separe por vírgula. (Obrigatório: 1-3)"
        );
    }

    private async Task ProcessGMStep_Keywords(SocketUserMessage message, GMContentCreationState state, string response)
    {
        var keywords = response.Split(',')
                               .Select(k => k.Trim())
                               .Where(k => !string.IsNullOrWhiteSpace(k))
                               .ToList();

        if (keywords.Count == 0 || keywords.Count > 3)
        {
            await message.Channel.SendMessageAsync("Você deve fornecer entre 1 e 3 Keywords, separadas por vírgula. Tente novamente.");
            return;
        }

        state.Keywords = keywords;

        state.CurrentStep = GMCreateStep.Finalize;

        await message.Channel.SendMessageAsync(
            "Keywords definidas. \n\n" +
            "**Etapa Final: Ataques e Inventário** (Ataques: Pág. 338)\n" +
            "1. Defina os **Ataques** (Dano e NA).\n" +
            "2. Defina o **Inventário/Abate**.\n\n" +
            "Digite a descrição completa dos Ataques, seguida por `|` e o Inventário.\n" +
            "Ex: `Machete: 4DC Brutal (FOR+7) | Armadura Invasor, 50 Tampas`"
        );
    }

    private async Task ProcessGMStep_Finalize(SocketUserMessage message, GMContentCreationState state, string response)
    {
        // 1. Extrair Ataques e Inventário
        var parts = response.Split('|');
        string ataques = parts.Length > 0 ? parts[0].Trim() : "ATAQUE DESARMADO: 2DC";
        string inventario = parts.Length > 1 ? parts[1].Trim() : "Nenhum";

        // Inicializa atributos com 5 caso seja Criatura (para evitar erro no cálculo)
        var attr = state.Attributes.Any() ? state.Attributes : new Dictionary<string, int> { { "FOR", 5 }, { "PER", 5 }, { "RES", 5 }, { "CAR", 5 }, { "INT", 5 }, { "AGI", 5 }, { "SOR", 5 } };
        int nivel = state.Level;
        string tipo = state.Type;

        // --- CÁLCULO DE PV --- (pág. 337)
        int pv_base = attr["RES"] + nivel;
        if (tipo.ToLower() == "notável")
            pv_base += attr["SOR"];
        else if (tipo.ToLower() == "principal")
            pv_base += (attr["SOR"] * 2);

        // --- CÁLCULO DE INICIATIVA --- (pág. 337)
        int iniciativa = attr["PER"] + attr["AGI"];
        if (tipo.ToLower() == "notável")
            iniciativa += 2;
        else if (tipo.ToLower() == "principal")
            iniciativa += 4;

        // --- CÁLCULO DE DEFESA --- (pág. 48)
        int defesa = attr["AGI"] >= 9 ? 2 : 1;

        // --- Dano CAC Bônus (Para Habilidades Especiais) ---
        string dano_cac_bonus;
        if (attr.ContainsKey("FOR"))
        {
            if (attr["FOR"] >= 11) dano_cac_bonus = "+3 DC";
            else if (attr["FOR"] >= 9) dano_cac_bonus = "+2 DC";
            else if (attr["FOR"] >= 7) dano_cac_bonus = "+1 DC";
            else dano_cac_bonus = "Nenhum";
        }
        else { dano_cac_bonus = "Nenhum"; }


        // 3. Montar a ficha de NPC para salvar
        var calculatedStats = new CreatureNPCStats
        {
            Nome = state.Name,
            Nivel = nivel,
            Tipo = tipo,
            PalavrasChave = string.Join(", ", state.Keywords),

            // Atributos (usando os valores ajustados para Criatura)
            FOR_Val = attr["FOR"],
            PER_Val = attr["PER"],
            RES_Val = attr["RES"],
            CAR_Val = attr["CAR"],
            INT_Val = attr["INT"],
            AGI_Val = attr["AGI"],
            SOR_Val = attr["SOR"],

            PV_Base = pv_base,
            Iniciativa_Base = iniciativa,
            Defesa = defesa,

            // RDs Padrão (Colocando 0 ou "0" por agora)
            RD_Fisico_Base = 0,
            RD_Energetico_Base = 0,
            RD_Radiativo_Base = "0",
            RD_Venenoso_Base = "0",

            Ataques = ataques,
            Inventario = inventario,
            Habilidades_Especiais = $"Dano CAC Bônus: {dano_cac_bonus}",
            FontePagina = state.Type == "NPC" ? 337 : 336
        };

        // 4. Salvar no Banco de Dados
        await _dbService.SaveCreatureNPCStatsAsync(state, calculatedStats);

        // 5. Retorno final (para o GM)
        var finalEmbed = new EmbedBuilder()
            .WithTitle($"✅ Conteúdo GM Salvo: {state.Name}!")
            .WithDescription($"A ficha de **{state.Name}** (Nvl {nivel}, Tipo {tipo}) foi salva no banco de dados e está pronta para uso.")
            .WithColor(Color.DarkGreen)
            .AddField("Estatísticas Chave (Calculadas)",
                      $"PV: **{pv_base}**\nIniciativa: **{iniciativa}**\nDefesa: **{defesa}**", true)
            .AddField("Keywords", string.Join(", ", state.Keywords), true)
            .AddField("Ataques (Rascunho)", ataques);

        _gmCreationCache.Remove(message.Author.Id);
        await message.Channel.SendMessageAsync(embed: finalEmbed.Build());
    }

}