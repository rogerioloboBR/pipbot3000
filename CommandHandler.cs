using Discord;
using Discord.WebSocket;

public class CommandHandler
{
    private readonly DiscordSocketClient _client;
    private readonly CharacterService _characterService;
    private readonly GameService _gameService;
    private readonly InfoService _infoService;
    private readonly CombatService _combatService;

    public CommandHandler(DiscordSocketClient client, CharacterService characterService,
                          GameService gameService, InfoService infoService,
                          CombatService combatService)
    {
        _client = client;
        _characterService = characterService;
        _gameService = gameService;
        _infoService = infoService;
        _combatService = combatService;
    }

    public Task InitializeAsync()
    {
        _client.MessageReceived += HandleMessageAsync;
        return Task.CompletedTask;
    }

    // --- ROTEADOR DE MENSAGENS (COMANDOS !) ---
    private async Task HandleMessageAsync(SocketMessage messageParam)
    {
        var message = messageParam as SocketUserMessage;
        if (message == null || message.Author.IsBot) return;

        ulong userId = message.Author.Id;

        // --- Roteamento de DM ---
        if (message.Channel is IDMChannel)
        {
            await _characterService.HandleDmCreationStepAsync(message);
            return;
        }

        // --- Roteamento de Servidor (Guild) ---
        var guildChannel = message.Channel as SocketGuildChannel;
        if (guildChannel == null) return;

        ulong guildId = guildChannel.Guild.Id;

        // Roteador de Comandos
        if (message.Content.StartsWith("!ping")) { await message.Channel.SendMessageAsync("Pong!"); }

        // Comandos de Jogo
        else if (message.Content.StartsWith("!teste")) { await _gameService.HandleTestCommandAsync(message, guildId, userId); }
        else if (message.Content.StartsWith("!dano")) { await _gameService.HandleDamageCommandAsync(message); }
        else if (message.Content.StartsWith("!pa")) { await _gameService.HandleActionPointCommandAsync(message, guildId); }
        else if (message.Content.StartsWith("!ps")) { await _gameService.HandleLuckPointCommandAsync(message, guildId, userId); }
        else if (message.Content.StartsWith("!rerrolar")) { await _gameService.HandleRerollCommandAsync(message, guildId, userId); }
        else if (message.Content.StartsWith("!fabricar") || message.Content.StartsWith("!craft"))
        {
            await _gameService.HandleCraftCommandAsync(message, guildId, userId);
        }
        else if (message.Content.StartsWith("!xp"))
        {
            await _gameService.HandleXPCommandAsync(message, guildId);
        }

        // Comandos de Ficha
        else if (message.Content.StartsWith("!registrar")) { await _characterService.HandleRegisterCommandAsync(message, guildId, userId); }
        else if (message.Content.StartsWith("!pericia")) { await _characterService.HandleSkillCommandAsync(message, guildId, userId); }
        else if (message.Content.StartsWith("!criar-personagem")) { await _characterService.HandleStartCreationCommandAsync(message, guildChannel.Guild); }

        // Comandos de Criação/Gestão do GM
        else if (message.Content.StartsWith("!gm-criar") || message.Content.StartsWith("!mestre-criar"))
        {
            await _characterService.HandleStartGMCreateCommandAsync(message, guildChannel.Guild);
        }

        // Comandos de Consulta (Informação)
        else if (message.Content.StartsWith("!ferimento")) { await _infoService.HandleInjuryCommandAsync(message); }
        else if (message.Content.StartsWith("!regra") || message.Content.StartsWith("!consulta")) { await _infoService.HandleRuleQueryCommandAsync(message); }
        else if (message.Content.StartsWith("!item") ||
                 message.Content.StartsWith("!arma") ||
                 message.Content.StartsWith("!armadura") ||
                 message.Content.StartsWith("!consumivel"))
        {
            await _infoService.HandleItemQueryAsync(message);
        }
        else if (message.Content.StartsWith("!mod"))
        {
            await _infoService.HandleModQueryAsync(message);
        }
        else if (message.Content.StartsWith("!vasculhar") || message.Content.StartsWith("!loot"))
        {
            await _infoService.HandleLootCommandAsync(message);
        }
        else if (message.Content.StartsWith("!npc"))
        {
            await _infoService.HandleNPCQueryAsync(message);
        }

        // Comandos de Combate
        else if (message.Content.StartsWith("!combate") || message.Content.StartsWith("!init"))
        {
            await _combatService.HandleCombatCommandAsync(message, guildId, userId);
        }
        else if (message.Content.StartsWith("!area") || message.Content.StartsWith("!hitloc"))
        {
            await _infoService.HandleAreaRollCommandAsync(message);
        }
        else if (message.Content.StartsWith("!gm") || message.Content.StartsWith("!mestre"))
        {
            await _infoService.HandleGMToolkitCommandAsync(message, guildId);
        }
    }

    // --- NOVO: ROTEADOR DE INTERAÇÕES (SLASH COMMANDS) ---
    public async Task HandleInteractionAsync(SocketInteraction interaction)
    {
        // Garante que o bot está interagindo com o Discord corretamente.
        if (interaction.Type == InteractionType.ApplicationCommand)
        {
            if (interaction is SocketSlashCommand slashCommand)
            {
                // Defer (Aguarde...) é necessário para comandos que podem demorar (consultas ao DB)
                await slashCommand.DeferAsync();
                await HandleSlashCommandAsync(slashCommand);
            }
        }
    }

    private async Task HandleSlashCommandAsync(SocketSlashCommand command)
    {
        ulong guildId = command.Channel is SocketGuildChannel guildChannel ? guildChannel.Guild.Id : 0;
        ulong userId = command.User.Id;

        // Este switch roteia os novos Slash Commands
        switch (command.CommandName)
        {
            case "teste":
                await HandleSlashTest(command, guildId, userId);
                break;
            case "npc":
                await HandleSlashNpc(command);
                break;
            case "regra":
                await HandleSlashRegra(command);
                break;
            case "area":
                await HandleSlashArea(command);
                break;
        }
    }

    // --- NOVOS MÉTODOS DE LÓGICA PARA SLASH COMMANDS ---

    private async Task HandleSlashTest(SocketSlashCommand command, ulong guildId, ulong userId)
    {
        // 1. Extrair opções
        var options = command.Data.Options;
        string skillName = (string)options.First(o => o.Name == "pericia").Value;
        int difficulty = (int)(long)options.First(o => o.Name == "dificuldade").Value;
        bool useLuck = options.FirstOrDefault(o => o.Name == "sorte")?.Value as bool? ?? false;
        int diceToBuy = (int)(options.FirstOrDefault(o => o.Name == "dados")?.Value as long? ?? 0);
        string username = (command.User as SocketGuildUser)?.Nickname ?? command.User.Username;

        // 2. Chamar o serviço refatorado
        var result = await _gameService.GetTestResultEmbedAsync(guildId, userId, username, skillName.ToLower(), difficulty, useLuck, diceToBuy, 0);

        // 3. Enviar a resposta
        if (result.Embed == null)
        {
            await command.FollowupAsync(result.ErrorMessage);
        }
        else
        {
            var sentMessage = await command.FollowupAsync(embed: result.Embed);
            // 4. Salvar no cache (se a rolagem foi bem-sucedida)
            if (result.RollCache != null)
            {
                result.RollCache.OriginalMessage = sentMessage;
                // Pedimos ao GameService para salvar o cache para nós:
                _gameService.CacheLastRoll(userId, result.RollCache);
            }
        }
    }

    private async Task HandleSlashNpc(SocketSlashCommand command)
    {
        string query = (string)command.Data.Options.First(o => o.Name == "nome").Value;
        var embed = await _infoService.GetNPCEmbedAsync(query);

        if (embed == null)
        {
            await command.FollowupAsync($"Criatura/NPC não encontrado para: `{query}`.");
        }
        else
        {
            await command.FollowupAsync(embed: embed);
        }
    }

    private async Task HandleSlashRegra(SocketSlashCommand command)
    {
        string query = (string)command.Data.Options.First(o => o.Name == "termo").Value;
        var embed = await _infoService.GetRuleEmbedAsync(query);
        await command.FollowupAsync(embed: embed);
    }

    private async Task HandleSlashArea(SocketSlashCommand command)
    {
        var embed = _infoService.GetAreaRollEmbed();
        await command.FollowupAsync(embed: embed);
    }
}